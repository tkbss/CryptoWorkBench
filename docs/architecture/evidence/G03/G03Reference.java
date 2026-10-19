// Test harness only. Production CryptoScript never uses a deterministic random source.
package org.bouncycastle.crypto.threshold;
import java.nio.file.*;
import java.security.SecureRandom;
import java.util.*;

public final class G03Reference {
    static final class FixedRandom extends SecureRandom {
        private int offset;
        public void nextBytes(byte[] output) {
            for (int i=0;i<output.length;i++) output[i]=(byte)(++offset * 73 + 19);
        }
    }
    public static void main(String[] args) throws Exception {
        var hex = HexFormat.of().withUpperCase();
        if (args[0].equals("generate")) {
            for (var algorithm : ShamirSecretSplitter.Algorithm.values()) {
                String profile = algorithm == ShamirSecretSplitter.Algorithm.AES ? "11B" : "11D";
                byte[] secret = hex.parseHex("000102030405060708090A0B0C0D0E0F80FF");
                var shares=ShamirSecretSplitter.getInstance(algorithm,secret.length,new FixedRandom()).resplit(secret,3,255).getSecretShares();
                for (int x : new int[]{1,3,17,128,255})
                    System.out.println(profile+"|"+hex.formatHex(secret)+"|3|255|"+x+"|"+hex.formatHex(shares[x-1].getEncoded()));
            }
        } else {
            int checked=0;
            for (String line:Files.readAllLines(Path.of(args[1]))) {
                if(line.isBlank()) continue;
                String[] fields=line.split("\\|");
                var algorithm=fields[0].equals("11B")?ShamirSecretSplitter.Algorithm.AES:ShamirSecretSplitter.Algorithm.RSA;
                List<ShamirSplitSecretShare> shares=new ArrayList<>();
                for(int i=2;i<fields.length;i++) {
                    String[] pair=fields[i].split(":");
                    shares.add(new ShamirSplitSecretShare(hex.parseHex(pair[1]),Integer.parseInt(pair[0])));
                }
                byte[] restored=ShamirSplitSecret.getInstance(algorithm,shares.toArray(new ShamirSplitSecretShare[0])).getSecret();
                if(!Arrays.equals(restored,hex.parseHex(fields[1]))) throw new AssertionError("Cross-test failed");
                checked++;
            }
            if(checked==0) throw new AssertionError("No C# vectors supplied");
            System.out.println("C# -> original BC Java: "+checked+" cases passed");
        }
    }
}
