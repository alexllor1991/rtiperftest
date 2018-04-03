/*
 * (c) 2005-2017  Copyright, Real-Time Innovations, Inc. All rights reserved.
 * Subject to Eclipse Public License v1.0; see LICENSE.md for details.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DDS;

namespace PerformanceTest
{
    public enum Transport
    {
        TRANSPORT_DEFAULT,
        TRANSPORT_UDPv4,
        TRANSPORT_UDPv6,
        TRANSPORT_TCPv4,
        TRANSPORT_TLSv4,
        TRANSPORT_DTLSv4,
        TRANSPORT_WANv4,
        TRANSPORT_SHMEM
    }

    public class TransportConfig
    {
        public TransportConfig()
        {
            kind = Transport.TRANSPORT_DEFAULT;
            nameString = string.Empty;
            prefixString = string.Empty;
        }

        public TransportConfig(
                Transport kind,
                string nameString,
                string prefixString)
        {
            this.kind = kind;
            this.nameString = nameString;
            this.prefixString = prefixString;
        }

        public Transport kind { set; get; }
        public string nameString { set; get; }
        public string prefixString { set; get; }
    }

    public class SecureTransportOptions
    {
        public SecureTransportOptions()
        {
            certAuthorityFile = string.Empty;
            certificateFile = string.Empty;
            privateKeyFile = string.Empty;
        }

        public string certAuthorityFile { set; get; }
        public string certificateFile { set; get; }
        public string privateKeyFile { set; get; }
    };

    public class TcpTransportOptions
    {
        public TcpTransportOptions()
        {
            serverBindPort = "7400";
            wanNetwork = false;
            publicAddress = "";
        }

        public string serverBindPort { set; get; }
        public bool wanNetwork { set; get; }
        public string publicAddress { set; get; }
    }

    public class WanTransportOptions
    {
        public WanTransportOptions()
        {
            wanServerAddress = "";
            wanServerPort = "3478";
            wanId = "";
            secureWan = false;
        }

        public string wanServerAddress { set; get; }
        public string wanServerPort  { set; get; }
        public string wanId { set; get; }
        public bool secureWan { set; get; }
    }

    public class PerftestTransport
    {
        /**************************************************************************/
        /* CLASS MEMBERS */

        private TransportConfig transportConfig;
        private string allowInterfaces = string.Empty;
        private string verbosity = string.Empty;
        // TCP specific options
        private TcpTransportOptions tcpOptions;
        // Security files
        private SecureTransportOptions secureOptions;
        // Wan specific options
        private WanTransportOptions wanOptions;

        private ulong dataLen = 100;
        private static readonly Dictionary<string, TransportConfig> transportConfigMap;

        // Tag used when adding logging output.
        private const string classLoggingString = "PerftestTransport:";

        // Default location of the security related files
        private const string TRANSPORT_PRIVATEKEY_FILE_PUB = "./resource/secure/pubkey.pem";
        private const string TRANSPORT_PRIVATEKEY_FILE_SUB = "./resource/secure/subkey.pem";
        private const string TRANSPORT_CERTIFICATE_FILE_PUB = "./resource/secure/pub.pem";
        private const string TRANSPORT_CERTIFICATE_FILE_SUB = "./resource/secure/sub.pem";
        private const string TRANSPORT_CERTAUTHORITY_FILE = "./resource/secure/cacert.pem";

        private string _LatencyTopicName;
        private string _AnnouncementTopicName;
        private string _ThroughputTopicName;
        
        
        public bool useMulticast;
        
        public string latencyMulticastAddr;
        public string announcementMulticastAddr;
        public string throughputMulticastAddr;
        
        public SortedDictionary<string, string> topicNameMap = 
            new SortedDictionary<string, string>();

        /**************************************************************************/
        /* CLASS CONSTRUCTOR AND DESTRUCTOR */

        static PerftestTransport()
        {
            transportConfigMap = new Dictionary<string, TransportConfig>()
            {
                { "Default", new TransportConfig(
                    Transport.TRANSPORT_DEFAULT,
                    "Default (UDPv4) / Custom (Taken from QoS profile)",
                    "dds.transport.UDPv4.builtin")},
                { "UDPv4", new TransportConfig(
                    Transport.TRANSPORT_UDPv4,
                    "UDPv4",
                    "dds.transport.UDPv4.builtin")},
                { "UDPv6", new TransportConfig(
                    Transport.TRANSPORT_UDPv6,
                    "UDPv6",
                    "dds.transport.UDPv6.builtin")},
                { "TCP", new TransportConfig(
                    Transport.TRANSPORT_TCPv4,
                    "TCP",
                    "dds.transport.TCPv4.tcp1")},
                { "TLS", new TransportConfig(
                    Transport.TRANSPORT_TLSv4,
                    "TLS",
                    "dds.transport.TCPv4.tcp1")},
                { "DTLS", new TransportConfig(
                    Transport.TRANSPORT_DTLSv4,
                    "DTLS",
                    "dds.transport.DTLS.dtls1")},
                { "WAN", new TransportConfig(
                    Transport.TRANSPORT_WANv4,
                    "WAN",
                    "dds.transport.WAN.wan1")},
                { "SHMEM", new TransportConfig(
                    Transport.TRANSPORT_SHMEM,
                    "SHMEM",
                    "dds.transport.shmem.builtin")}
            };
        }
        
        public PerftestTransport()
        {
            transportConfig = new TransportConfig();
            tcpOptions = new TcpTransportOptions();
            secureOptions = new SecureTransportOptions();
            wanOptions = new WanTransportOptions();
        
            useMulticast = false;
            latencyMulticastAddr = "239.255.1.2";
            announcementMulticastAddr = "239.255.1.100";
            throughputMulticastAddr = "239.255.1.1";
            _LatencyTopicName = "Latency";
            _AnnouncementTopicName = "Announcement";
            _ThroughputTopicName = "Throughput";
            
            topicNameMap.Add(_LatencyTopicName, latencyMulticastAddr);
            topicNameMap.Add(_AnnouncementTopicName, announcementMulticastAddr);
            topicNameMap.Add(_ThroughputTopicName, throughputMulticastAddr);
        }

        /**************************************************************************/

        public static Dictionary<string, int> GetTransportCmdLineArgs()
        {

            Dictionary<string, int> cmdLineArgsMap = new Dictionary<string, int>();

            cmdLineArgsMap.Add("-transport", 1);
            cmdLineArgsMap.Add("-enableTCP", 0);
            cmdLineArgsMap.Add("-enableUDPv6", 0);
            cmdLineArgsMap.Add("-enableSharedMemory", 0);
            cmdLineArgsMap.Add("-nic", 1);
            cmdLineArgsMap.Add("-allowInterfaces", 1);
            cmdLineArgsMap.Add("-transportServerBindPort", 1);
            cmdLineArgsMap.Add("-transportVerbosity", 1);
            cmdLineArgsMap.Add("-transportWan", 0);
            cmdLineArgsMap.Add("-transportPublicAddress", 1);
            cmdLineArgsMap.Add("-transportCertAuthority", 1);
            cmdLineArgsMap.Add("-transportCertFile", 1);
            cmdLineArgsMap.Add("-transportPrivateKey", 1);
            cmdLineArgsMap.Add("-transportWanServerAddress", 1);
            cmdLineArgsMap.Add("-transportWanServerPort", 1);
            cmdLineArgsMap.Add("-transportWanId", 1);
            cmdLineArgsMap.Add("-transportSecureWan", 0);
            cmdLineArgsMap.Add("-multicast", 1);
            cmdLineArgsMap.Add("-nomulticast", 0);

            return cmdLineArgsMap;
        }

        public string HelpMessageString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\t===================== Transport Specific Options ======================\n");
            sb.Append("\n");
            sb.Append("\t-transport <kind>             - Set transport to be used. The rest of\n");
            sb.Append("\t                                the transports will be disabled.\n");
            sb.Append("\t                                Values:\n");
            sb.Append("\t                                    UDPv4\n");
            sb.Append("\t                                    UDPv6\n");
            sb.Append("\t                                    SHMEM\n");
            sb.Append("\t                                    TCP\n");
            sb.Append("\t                                    TLS\n");
            sb.Append("\t                                    DTLS\n");
            sb.Append("\t                                    WAN\n");
            sb.Append("\t                                Default: UDPv4\n");
            sb.Append("\t-nic <ipaddr>                 - Use only the nic specified by <ipaddr>.\n");
            sb.Append("\t                                If not specified, use all available\n");
            sb.Append("\t                                interfaces\n");
            sb.Append("\t-multicast <address>          - Use multicast to send data.\n");
            sb.Append("\t                                Default not to use multicast\n");
            sb.Append("\t                                <address> is optional, if unspecified:\n");
            sb.Append("\t                                                latency 239.255.1.2,\n");
            sb.Append("\t                                                announcement 239.255.1.100,\n");
            sb.Append("\t                                                throughput 239.255.1.1\n");
            sb.Append("\t-transportVerbosity <level>   - Verbosity of the transport\n");
            sb.Append("\t                                Default: 0 (errors only)\n");
            sb.Append("\t-transportServerBindPort <p>  - Port used by the transport to accept\n");
            sb.Append("\t                                TCP/TLS connections <optional>\n");
            sb.Append("\t                                Default: 7400\n");
            sb.Append("\t-transportWan                   Use TCP/TLS across LANs and Firewalls.\n");
            sb.Append("\t                                Default: Not Set, LAN mode.\n");
            sb.Append("\t-transportPublicAddress <ip>  - Public IP address and port (WAN address\n");
            sb.Append("\t                                and port) (separated with �:� ) related\n");
            sb.Append("\t                                to the transport instantiation. This is\n");
            sb.Append("\t                                required when using server mode.\n");
            sb.Append("\t                                Default: Not Set.\n");
            sb.Append("\t-transportWanServerAddress <a>- Address where to find the WAN Server\n");
            sb.Append("\t                                Default: Not Set (Required)\n");
            sb.Append("\t-transportWanServerPort <p>     Port where to find the WAN Server.\n");
            sb.Append("\t                                Default: 3478.\n");
            sb.Append("\t-transportWanId <id>          - Id to be used for the WAN transport.\n");
            sb.Append("\t                                Default: Not Set (Required).\n");
            sb.Append("\t-transportSecureWan           - Use WAN with security.\n");
            sb.Append("\t                                Default: False.\n");
            sb.Append("\t-transportCertAuthority <file>- Certificate authority file <optional>\n");
            sb.Append("\t                                Default: \"");
            sb.Append(TRANSPORT_CERTAUTHORITY_FILE).Append("\"\n");
            sb.Append("\t-transportCertFile <file>     - Certificate file <optional>\n");
            sb.Append("\t                                Default (Publisher): \"");
            sb.Append(TRANSPORT_CERTIFICATE_FILE_PUB).Append("\"\n");
            sb.Append("\t                                Default (Subscriber): \"");
            sb.Append(TRANSPORT_CERTIFICATE_FILE_SUB).Append("\"\n");
            sb.Append("\t-transportPrivateKey <file>   - Private key file <optional>\n");
            sb.Append("\t                                Default (Publisher): \"");
            sb.Append(TRANSPORT_PRIVATEKEY_FILE_PUB).Append("\"\n");
            sb.Append("\t                                Default (Subscriber): \"");
            sb.Append(TRANSPORT_PRIVATEKEY_FILE_SUB).Append("\"\n");

            return sb.ToString();
        }

        public void PrintTransportConfigurationSummary()
        {

            StringBuilder sb = new StringBuilder("Transport Information:\n");
            sb.Append("\tKind: ").Append(transportConfig.nameString).Append("\n");

            if (!string.IsNullOrEmpty(allowInterfaces))
            {
                sb.Append("\tNic: ").Append(allowInterfaces).Append("\n");
            }

            sb.Append( "\tUse Multicast: ").Append((AllowsMulticast())? "True" : "False");
            if(!AllowsMulticast() && useMulticast){
                sb.Append ("  (Multicast is not supported for " );
                sb.Append( transportConfig.nameString ).Append(")\n");
            }
            sb.Append( "\n");

            if (transportConfig.kind == Transport.TRANSPORT_TCPv4
                    || transportConfig.kind == Transport.TRANSPORT_TLSv4)
            {
                sb.Append("\tTCP Server Bind Port: ").Append(tcpOptions.serverBindPort).Append("\n");
                sb.Append("\tTCP LAN/WAN mode: ");
                if (tcpOptions.wanNetwork)
                {
                    sb.Append("WAN\n");
                }
                else
                {
                    sb.Append("LAN\n");
                }
                if (tcpOptions.wanNetwork)
                {
                    sb.Append("\tTCP Public Address: ").Append(tcpOptions.publicAddress).Append("\n");
                }
            }

            if (transportConfig.kind == Transport.TRANSPORT_WANv4)
            {

                sb.Append("\tWAN Server Address: ");
                sb.Append(wanOptions.wanServerAddress).Append(":");
                sb.Append(wanOptions.wanServerPort).Append("\n");

                sb.Append("\tWAN Id: ").Append(wanOptions.wanId).Append("\n");

                sb.Append("\tWAN Secure: ").Append(wanOptions.secureWan).Append("\n");
            }

            if (transportConfig.kind == Transport.TRANSPORT_TLSv4
                    || transportConfig.kind == Transport.TRANSPORT_DTLSv4
                    || (transportConfig.kind == Transport.TRANSPORT_WANv4 && wanOptions.secureWan))
            {

                sb.Append("\tCertificate authority file: ").Append(secureOptions.certAuthorityFile).Append("\n");
                sb.Append("\tCertificate file: ").Append(secureOptions.certificateFile).Append("\n");
                sb.Append("\tPrivate key file: ").Append(secureOptions.privateKeyFile).Append("\n");
            }

            if (!string.IsNullOrEmpty(verbosity))
            {
                sb.Append("\tVerbosity: ").Append(verbosity).Append("\n");
            }

            Console.Error.Write(sb.ToString());
        }

        public bool AllowsMulticast()
        {
            return (transportConfig.kind != Transport.TRANSPORT_TCPv4
                    && transportConfig.kind != Transport.TRANSPORT_TLSv4
                    && transportConfig.kind != Transport.TRANSPORT_WANv4
                    && transportConfig.kind != Transport.TRANSPORT_SHMEM
                    && useMulticast);
        }

        public bool ParseTransportOptions(string[] argv)
        {
            bool isPublisher = false;
            string transportString = "Default";

            for (int i = 0; i < argv.Length; ++i)
            {
                if (string.IsNullOrEmpty(argv[i]))
                {
                    continue;
                }

                if ("-pub".StartsWith(argv[i], true, null))
                {
                    isPublisher = true;
                }
                else if ("-dataLen".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <length> after -dataLen");
                        return false;
                    }
                    if (!UInt64.TryParse(argv[i], out dataLen))
                    {
                        Console.Error.Write("Bad dataLen\n");
                        return false;
                    }
                }
                else if ("-transport".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <kind> after -transport");
                        return false;
                    }
                    transportString = argv[i];
                }
                else if ("-enableTCP".StartsWith(argv[i], true, null))
                {
                    // Legacy option
                    transportString = "TCP";
                }
                else if ("-enableUDPv6".StartsWith(argv[i], true, null))
                {
                    // Legacy option
                    transportString = "UDPv6";
                }
                else if ("-enableSharedMemory".StartsWith(argv[i], true, null))
                {
                    // Legacy option
                    transportString = "SHMEM";
                }
                else if ("-nic".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <address> after -nic");
                        return false;
                    }
                    allowInterfaces = argv[i];
                }
                else if ("-allowInterfaces".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <address> after -allowInterfaces");
                        return false;
                    }
                    allowInterfaces = argv[i];
                }
                else if ("-transportVerbosity".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <level> after -transportVerbosity");
                        return false;
                    }
                    verbosity = argv[i];
                }
                else if ("-transportServerBindPort".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <number> after -transportServerBindPort");
                        return false;
                    }
                    tcpOptions.serverBindPort = argv[i];
                }
                else if ("-transportWan".StartsWith(argv[i], true, null))
                {
                    tcpOptions.wanNetwork = true;
                }
                else if ("-transportPublicAddress".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <address> after -transportPublicAddress");
                        return false;
                    }
                    tcpOptions.publicAddress = argv[i];
                }
                else if ("-transportPublicAddress".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <address> after -transportPublicAddress");
                        return false;
                    }
                    tcpOptions.publicAddress = argv[i];
                }
                else if ("-transportCertAuthority".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing Missing <path> after -transportCertAuthority");
                        return false;
                    }
                    secureOptions.certAuthorityFile = argv[i];
                }
                else if ("-transportCertFile".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <path> after -transportCertFile");
                        return false;
                    }
                    secureOptions.certificateFile = argv[i];
                }
                else if ("-transportPrivateKey".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <path> after -transportPrivateKey");
                        return false;
                    }
                    secureOptions.privateKeyFile = argv[i];
                }
                else if ("-transportWanServerAddress".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <address> after -transportWanServerAddress");
                        return false;
                    }
                    wanOptions.wanServerAddress = argv[i];
                }
                else if ("-transportWanServerPort".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <port> after -transportWanServerPort");
                        return false;
                    }
                    wanOptions.wanServerPort = argv[i];
                }
                else if ("-transportWanId".StartsWith(argv[i], true, null))
                {
                    if ((i == (argv.Length - 1)) || argv[++i].StartsWith("-"))
                    {
                        Console.Error.Write(classLoggingString
                                + " Missing <id> after -transportWanId");
                        return false;
                    }
                    wanOptions.wanId = argv[i];
                }
                else if ("-transportSecureWan".StartsWith(argv[i], true, null))
                {
                    wanOptions.secureWan = true;
                }
                else if ("-multicast".StartsWith(argv[i], true, null))
                {
                    useMulticast = true;
                    if ((i != (argv.Length - 1)) && !argv[1+i].StartsWith("-"))
                    {
                        i++;
                        throughputMulticastAddr = argv[i];
                        latencyMulticastAddr = argv[i];
                        announcementMulticastAddr = argv[i];
                    }
                }
                else if ("-nomulticast".StartsWith(argv[i], true, null))
                {
                    useMulticast = false;
                }
            }

            if (!setTransport(transportString))
            {
                Console.Error.Write(classLoggingString + " Error Setting the transport");
                return false;
            }

            // We only need to set the secure properties for this
            if (transportConfig.kind == Transport.TRANSPORT_TLSv4
                    || transportConfig.kind == Transport.TRANSPORT_DTLSv4
                    || transportConfig.kind == Transport.TRANSPORT_WANv4)
            {

                PopulateSecurityFiles(isPublisher);
            }
            return true;
        }

        /**************************************************************************/

        private bool setTransport(String transportString)
        {

            try
            {
                transportConfig = transportConfigMap[transportString];
            }
            catch (KeyNotFoundException)
            {
                Console.Error.Write(classLoggingString + " \"" 
                        + transportString + "\" is not a valid transport. "
                        + "List of supported transport:");
                Dictionary<string, TransportConfig>.KeyCollection 
                            transportNameList = transportConfigMap.Keys;
                foreach (string transportName in transportNameList)
                {
                    Console.Error.Write("\t\"" + transportName + "\"");
                }
                return false;
            }

            return true;
        }

        private void PopulateSecurityFiles(bool isPublisher)
        {

            if (string.IsNullOrEmpty(secureOptions.certificateFile))
            {
                if (isPublisher)
                {
                    secureOptions.certificateFile = TRANSPORT_CERTIFICATE_FILE_PUB;
                }
                else
                {
                    secureOptions.certificateFile = TRANSPORT_CERTIFICATE_FILE_SUB;
                }
            }

            if (string.IsNullOrEmpty(secureOptions.privateKeyFile))
            {
                if (isPublisher)
                {
                    secureOptions.privateKeyFile = TRANSPORT_PRIVATEKEY_FILE_PUB;
                }
                else
                {
                    secureOptions.privateKeyFile = TRANSPORT_PRIVATEKEY_FILE_SUB;
                }
            }

            if (string.IsNullOrEmpty(secureOptions.certAuthorityFile))
            {
                secureOptions.certAuthorityFile = TRANSPORT_CERTAUTHORITY_FILE;
            }
        }

        /**************************************************************************/

        private void SetAllowInterfacesList(DDS.DomainParticipantQos qos)
        {
            if (!string.IsNullOrEmpty(allowInterfaces))
            {
                /*
                 * By default, if the transport is not set, it should be UDPv4, if it is not
                 * It means that we have modified the QOS, so we won't use the -nic param.
                 */
                if (transportConfig.kind == Transport.TRANSPORT_DEFAULT
                        && qos.transport_builtin.mask 
                            != (int) DDS.TransportBuiltinKind.TRANSPORTBUILTIN_UDPv4) {
                    Console.Error.Write(classLoggingString
                           + " Ignoring -nic option, Transport has been modified via QoS");
                    return;
                }

                string propertyName = transportConfig.prefixString;

                if (transportConfig.kind == Transport.TRANSPORT_WANv4)
                {
                    propertyName += ".parent";
                }

                propertyName += ".parent.allow_interfaces";

                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        propertyName,
                        allowInterfaces,
                        false);
            }
        }

        private void SetTransportVerbosity(DDS.DomainParticipantQos qos)
        {

            if (!string.IsNullOrEmpty(verbosity))
            {
                /*
                 * By default, if the transport is not set, it should be UDPv4,
                 * if it is not it means that we have modified the QOS, so we won't
                 * use the -transportVerbosity param.
                 */
                 if (transportConfig.kind == Transport.TRANSPORT_DEFAULT
                         && qos.transport_builtin.mask 
                             != (int) DDS.TransportBuiltinKind.TRANSPORTBUILTIN_UDPv4) {
                     Console.Error.Write(classLoggingString
                            + " Ignoring -transportVerbosity option, Transport has been modified via QoS");
                     return;
                 }

                string propertyName = transportConfig.prefixString + ".verbosity";

                // The name of the property in TCPv4 is different
                if (transportConfig.kind == Transport.TRANSPORT_TCPv4)
                {
                    propertyName = transportConfig.prefixString + ".logging_verbosity_bitmap";
                }
                else if (transportConfig.kind == Transport.TRANSPORT_UDPv4
                      || transportConfig.kind == Transport.TRANSPORT_UDPv6
                      || transportConfig.kind == Transport.TRANSPORT_SHMEM)
                {
                    // We do not change logging for the builtin transports.
                    return;
                }

                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        propertyName,
                        verbosity,
                        false);
            }
        }

        private void ConfigureSecurityFiles(DDS.DomainParticipantQos qos)
        {

            if (!string.IsNullOrEmpty(secureOptions.certAuthorityFile))
            {
                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".tls.verify.ca_file",
                        secureOptions.certAuthorityFile,
                        false);
            }

            if (!string.IsNullOrEmpty(secureOptions.certificateFile))
            {
                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".tls.identity.certificate_chain_file",
                        secureOptions.certificateFile,
                        false);
            }

            if (!string.IsNullOrEmpty(secureOptions.privateKeyFile))
            {
                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".tls.identity.private_key_file",
                        secureOptions.privateKeyFile,
                        false);
            }
        }

        private bool ConfigureTcpTransport(DDS.DomainParticipantQos qos) {

            qos.transport_builtin.mask = (int) DDS.TransportBuiltinKindMask.TRANSPORTBUILTIN_MASK_NONE;

            DDS.PropertyQosPolicyHelper.add_property(
                    qos.property_qos,
                    "dds.transport.load_plugins",
                    transportConfig.prefixString,
                    false);

            if (!string.IsNullOrEmpty(tcpOptions.serverBindPort))
            {
                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".server_bind_port",
                        tcpOptions.serverBindPort,
                        false);
            }

            if (tcpOptions.wanNetwork)
            {
                DDS.PropertyQosPolicyHelper.assert_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".parent.classid",
                        "NDDS_TRANSPORT_CLASSID_TCPV4_WAN",
                        false);

                if (tcpOptions.serverBindPort != "0") 
                {
                    if (!string.IsNullOrEmpty(tcpOptions.publicAddress))
                    {
                        DDS.PropertyQosPolicyHelper.add_property(
                                qos.property_qos,
                                transportConfig.prefixString + ".public_address",
                                tcpOptions.publicAddress,
                                false);
                    }
                    else
                    {
                        Console.Error.Write(classLoggingString 
                                + " Public Address is required if Server Bind Port != 0");
                        return false;
                    }
                }
            }

            if (transportConfig.kind == Transport.TRANSPORT_TLSv4)
            {
                if (tcpOptions.wanNetwork)
                {
                    DDS.PropertyQosPolicyHelper.assert_property(
                            qos.property_qos,
                            transportConfig.prefixString + ".parent.classid",
                            "NDDS_TRANSPORT_CLASSID_TLSV4_WAN",
                            false);
                }
                else 
                {
                    DDS.PropertyQosPolicyHelper.assert_property(
                            qos.property_qos,
                            transportConfig.prefixString + ".parent.classid",
                            "NDDS_TRANSPORT_CLASSID_TLSV4_LAN",
                            false);
                }

                ConfigureSecurityFiles(qos);
            }

            return true;
        }

        private void ConfigureDtlsTransport(DDS.DomainParticipantQos qos) {

            qos.transport_builtin.mask = (int) DDS.TransportBuiltinKindMask.TRANSPORTBUILTIN_MASK_NONE;

                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        "dds.transport.load_plugins",
                        transportConfig.prefixString,
                        false);

                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".library",
                        "nddstransporttls",
                        false);

                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".create_function",
                        "NDDS_Transport_DTLS_create",
                        false);

            ConfigureSecurityFiles(qos);
        }

        private bool ConfigureWanTransport(DDS.DomainParticipantQos qos) {

            qos.transport_builtin.mask = (int) DDS.TransportBuiltinKindMask.TRANSPORTBUILTIN_MASK_NONE;

                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        "dds.transport.load_plugins",
                        transportConfig.prefixString,
                        false);

                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".library",
                        "nddstransportwan",
                        false);

                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".create_function",
                        "NDDS_Transport_WAN_create",
                        false);

            if (!string.IsNullOrEmpty(wanOptions.wanServerAddress))
            {
                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".server",
                        wanOptions.wanServerAddress,
                        false);
            }
            else 
            {
                Console.Error.Write(classLoggingString + " Wan Server Address is required");
                return false;
            }

            if (!string.IsNullOrEmpty(wanOptions.wanServerPort))
            {
                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".server_port",
                        wanOptions.wanServerPort,
                        false);
            }

            if (!string.IsNullOrEmpty(wanOptions.wanId))
            {
                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".transport_instance_id",
                        wanOptions.wanId,
                        false);
            } 
            else 
            {
                Console.Error.Write(classLoggingString + " Wan ID is required");
                return false;
            }

            if (wanOptions.secureWan) 
            {
                DDS.PropertyQosPolicyHelper.add_property(
                        qos.property_qos,
                        transportConfig.prefixString + ".enable_security",
                        "1",
                        false);

                ConfigureSecurityFiles(qos);
            }

            return true;
        }

        private void ConfigureShmemTransport(DDS.DomainParticipantQos qos) {

            qos.transport_builtin.mask = (int) DDS.TransportBuiltinKind.TRANSPORTBUILTIN_SHMEM;

            // SHMEM transport properties
            int receivedMessageCountMax = 2 * 1024 * 1024 / (int) dataLen;
            if (receivedMessageCountMax < 1) {
                receivedMessageCountMax = 1;
            }

            DDS.PropertyQosPolicyHelper.add_property(
                    qos.property_qos,
                    "dds.transport.shmem.builtin.received_message_count_max",
                    receivedMessageCountMax.ToString(),
                    false);
        }

        public bool ConfigureTransport(DDS.DomainParticipantQos qos) {

            switch (transportConfig.kind) {

            case Transport.TRANSPORT_DEFAULT:
                // We do not set any transport_builtin mask, either is UDPv4 or is something custom writen in the xml.
                break;

            case Transport.TRANSPORT_UDPv4:
                qos.transport_builtin.mask = (int) DDS.TransportBuiltinKind.TRANSPORTBUILTIN_UDPv4;
                break;

            case Transport.TRANSPORT_UDPv6:
                qos.transport_builtin.mask = (int) DDS.TransportBuiltinKind.TRANSPORTBUILTIN_UDPv6;
                break;

            case Transport.TRANSPORT_SHMEM:
                ConfigureShmemTransport(qos);
                break;

            case Transport.TRANSPORT_TCPv4:
                if (!ConfigureTcpTransport(qos)) {
                    Console.Error.Write(classLoggingString + " Failed to configure TCP plugin");
                    return false;
                }
                break;

            case Transport.TRANSPORT_TLSv4:
                if (!ConfigureTcpTransport(qos)) {
                    Console.Error.Write(classLoggingString + " Failed to configure TCP - TLS plugin");
                    return false;
                }
                break;

            case Transport.TRANSPORT_DTLSv4:
                ConfigureDtlsTransport(qos);
                break;

            case Transport.TRANSPORT_WANv4:
                if (!ConfigureWanTransport(qos)) {
                    Console.Error.Write(classLoggingString + " Failed to configure WAN plugin");
                    return false;
                }
                break;

            default:
                Console.Error.Write(classLoggingString + " Transport is not supported");
                return false;

            } // Switch

            if (transportConfig.kind != Transport.TRANSPORT_SHMEM) {
                SetAllowInterfacesList(qos);
            } else {
                // We are not using the allow interface string, so we clear it
                allowInterfaces = string.Empty;
            }

            SetTransportVerbosity(qos);

            return true;
        }
        
        public string getMulticastAddr(string topic)
        {
            return topicNameMap[topic];
        }
        
    }

} // Perftest Namespace
