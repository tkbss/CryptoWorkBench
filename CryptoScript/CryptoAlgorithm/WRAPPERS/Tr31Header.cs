using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS
{
    public class Tr31BlockHeader : CryptoAlgorithm
    {
        
        public override BlockHeaderVariableDeclaration GenerateBlockHeader(string[] parameters)
        {
            return new BlockHeaderVariableDeclaration();
        }
        public override BlockHeaderVariableDeclaration GenerateBlockHeader(string mechanism)
        {
            KeyBlockVersionID = "D";
            KeyBlockLength = "112";
            KeyUsage = "D0";
            Algorithm = "A";
            ModeOfUse = "B";
            KeyVersionNumber = "00";
            Exportability = 'S';
            KeyContext = '0';
            NumberOfOptionalBlocks = "00";
            return Create();
        }
        private BlockHeaderVariableDeclaration Create()
        {
            var blockHeader = new BlockHeaderVariableDeclaration
            {
                Type = new CryptoTypeTR31Header()
            };
            string v = "{" + FIELD_NAMES[0] + ":" + KeyBlockVersionID +
                "," + FIELD_NAMES[1] + ":" + KeyBlockLength +
                "," + FIELD_NAMES[2] + ":" + KeyUsage +
                "," + FIELD_NAMES[3] + ":" + Algorithm +
                "," + FIELD_NAMES[4] + ":" + ModeOfUse +
                "," + FIELD_NAMES[5] + ":" + KeyVersionNumber +
                "," + FIELD_NAMES[6] + ":" + Exportability +
                "," + FIELD_NAMES[7] + ":" + KeyContext +
                "," + FIELD_NAMES[8] + ":" + NumberOfOptionalBlocks +
                "," + FIELD_NAMES[9] + ":" + ReservedField +
                "}";
            blockHeader.Value = v;
            return blockHeader;
        }
        private readonly string[] FIELD_NAMES=new string[]{"KBVID", "KBLEN", "KEYU", "ALGO", "MODEU", "KEYVN", "EXP", "KEYCTX", "NUMOPTB", "RSV", "OPTID", "OPTBD", "WKL" };    
        private static readonly HashSet<string> AllowedVersions = new() { "A", "B", "C", "D" };
        private static readonly HashSet<string> AllowedKeyUsages = new()
        {
            "B0", "B1", "B2", "B3", "C0", "D0", "D1", "D2", "D3",
            "E0", "E1", "E2", "E3", "F3", "F4", "F5", "F6", "H",
            "I0", "K0", "K1", "K2", "K3", "K4", "M0", "M1", "P2",
            "R", "S", "S0", "S1", "S2", "T", "V0", "V1", "V2",
            "V3", "V4", "V5"
        };
        private static readonly HashSet<string> AllowedAlgorithms = new() { "A", "D", "E", "H", "R", "S", "T" };
        private static readonly HashSet<string> AllowedModesOfUse = new() { "B", "C", "D", "E", "G", "N", "S", "T", "V", "X", "Y" };
        private static readonly HashSet<char> AllowedExportability = new() { 'E', 'N', 'S' };
        private static readonly HashSet<char> AllowedKeyContext = new() { '0', '1', '2' };

        private string? _keyBlockVersionID;
        public string KeyBlockVersionID
        {
            get => _keyBlockVersionID;
            set
            {
                if (!AllowedVersions.Contains(value))
                    throw new ArgumentException($"Invalid Key Block Version ID: {value}");
                _keyBlockVersionID = value;
            }
        }
        string? _keyBlockLength;
        public string? KeyBlockLength 
        { 
            get=>_keyBlockLength;
            set 
            {                
                _keyBlockLength = value.PadLeft(4, '0');
            } 
        }

        private string? _keyUsage;
        public string KeyUsage
        {
            get => _keyUsage;
            set
            {
                if (!AllowedKeyUsages.Contains(value))
                    throw new ArgumentException($"Invalid Key Usage: {value}");
                _keyUsage = value;
            }
        }

        private string? _algorithm;
        public string Algorithm
        {
            get => _algorithm;
            set
            {
                if (!AllowedAlgorithms.Contains(value))
                    throw new ArgumentException($"Invalid Algorithm: {value}");
                _algorithm = value;
            }
        }

        private string? _modeOfUse;
        public string ModeOfUse
        {
            get => _modeOfUse;
            set
            {
                if (!AllowedModesOfUse.Contains(value))
                    throw new ArgumentException($"Invalid Mode of Use: {value}");
                _modeOfUse = value;
            }
        }

        public string? KeyVersionNumber { get; set; }

        private char _exportability;
        public char Exportability
        {
            get => _exportability;
            set
            {
                if (!AllowedExportability.Contains(value))
                    throw new ArgumentException($"Invalid Exportability: {value}");
                _exportability = value;
            }
        }

        public string? NumberOfOptionalBlocks { get; set; }

        private char _keyContext;
        public char KeyContext
        {
            get => _keyContext;
            set
            {
                if (!AllowedKeyContext.Contains(value))
                    throw new ArgumentException($"Invalid Key Context: {value}");
                _keyContext = value;
            }
        }

        public char ReservedField { get; set; } = '0';

        /// <summary>
        /// Optional blocks associated with this key block header.
        /// </summary>
        public List<OptionalBlock> OptionalBlocks { get; set; } = new();

        

        /// <summary>
        /// Adds an optional block to the header.
        /// </summary>
        public void AddOptionalBlock(string id, string data)
        {
            OptionalBlocks.Add(new OptionalBlock
            {
                ID = id,
                Data = data,
                Length = data.Length + 4
            });
            NumberOfOptionalBlocks = OptionalBlocks.Count.ToString().PadLeft(2,'0');
        }
    }

    /// <summary>
    /// Represents an Optional Block as defined by TR-31 standard.
    /// </summary>
    
    

}
