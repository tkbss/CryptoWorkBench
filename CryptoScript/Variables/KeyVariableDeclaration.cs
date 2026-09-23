using CryptoScript.CryptoAlgorithm.WRAPPERS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace CryptoScript.Variables
{
    public class KeyVariableDeclaration : VariableDeclaration
    {
        private KeyType _keyType = KeyType.Secret(KeyAlgorithm.Unknown);
        private KeyUsagePolicy _usage = KeyUsagePolicy.Unspecified;
        private KeySize _keySizeInBits = global::CryptoScript.Variables.KeySize.Unknown;
        private string _keySize = string.Empty;

        public string DerivationMechanism { get; set; } = string.Empty;
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public KeyType KeyType
        {
            get => _keyType;
            set => _keyType = value ?? throw new ArgumentNullException(nameof(value));
        }
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public KeyUsagePolicy Usage
        {
            get => _usage;
            set => _usage = value ?? throw new ArgumentNullException(nameof(value));
        }
        public KeySize KeySizeInBits
        {
            get => _keySizeInBits;
            set
            {
                if (_keySizeInBits.IsKnown && _keySizeInBits != value)
                    throw new ArgumentException("KeySizeInBits conflicts with the existing key size.", nameof(value));

                _keySizeInBits = value;
                if (value.IsKnown)
                    _keySize = value.Bits.ToString(CultureInfo.InvariantCulture);
            }
        }
        public string KeySize
        {
            get => _keySize;
            set
            {
                value ??= string.Empty;
                if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong bits) && bits > 0)
                {
                    var typedSize = new KeySize(bits);
                    if (_keySizeInBits.IsKnown && _keySizeInBits != typedSize)
                        throw new ArgumentException("KeySize conflicts with KeySizeInBits.", nameof(value));
                    _keySizeInBits = typedSize;
                }
                else if (_keySizeInBits.IsKnown)
                {
                    throw new ArgumentException("KeySize must represent the known KeySizeInBits value.", nameof(value));
                }

                _keySize = value;
            }
        }
        public string KeyValue { get; set; } = string.Empty;
        public List<OptionalBlock> KeyAttributes { get; set; } = new List<OptionalBlock>();
        //public override string Value { get => base.Value; set => base.Value = value; }
        public override string PrintOutput()
        {
            return base.PrintOutput();
        }
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static KeyVariableDeclaration Deserialize(string json)
        {
            var jsonObject = JObject.Parse(json);
            bool hasKeyType = jsonObject.Properties()
                .Any(property => property.Name.Equals(nameof(KeyType), StringComparison.OrdinalIgnoreCase));
            JProperty? legacyMechanismProperty = jsonObject.Properties()
                .FirstOrDefault(property => property.Name.Equals("Mechanism", StringComparison.OrdinalIgnoreCase));
            string legacyMechanism = legacyMechanismProperty?.Value.Type == JTokenType.String
                ? legacyMechanismProperty.Value.Value<string>() ?? string.Empty
                : string.Empty;
            legacyMechanismProperty?.Remove();

            var key = jsonObject.ToObject<KeyVariableDeclaration>()
                ?? throw new JsonSerializationException("KEY JSON did not contain a key object.");

            if (!hasKeyType)
                key.KeyType = InferLegacyKeyType(legacyMechanism);

            return key;
        }

        private static KeyType InferLegacyKeyType(string mechanism)
        {
            if (mechanism.StartsWith("AES-", StringComparison.OrdinalIgnoreCase))
                return KeyType.Secret(KeyAlgorithm.Aes);
            if (mechanism.StartsWith("DES3-", StringComparison.OrdinalIgnoreCase))
                return KeyType.Secret(KeyAlgorithm.Tdea);
            if (mechanism.StartsWith("HMAC-", StringComparison.OrdinalIgnoreCase))
                return KeyType.Secret(KeyAlgorithm.Hmac);
            return KeyType.Secret(KeyAlgorithm.Unknown);
        }
    }
}
