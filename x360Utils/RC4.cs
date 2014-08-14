namespace x360Utils {
    public static class Rc4 {
        public static void Compute(ref byte[] data, byte[] key) {
            var s = new byte[256];
            var k = new byte[256];
            byte temp;
            int i;
            var j = 0;
            for(i = 0; i < 256; i++) {
                s[i] = (byte)i;
                k[i] = key[i % key.GetLength(0)];
                j = (j + s[i] + k[i]) % 256;
                temp = s[i];
                s[i] = s[j];
                s[j] = temp;
            }
            i = j = 0;
            for(var x = 0; x < data.GetLength(0); x++) {
                i = (i + 1) % 256;
                j = (j + s[i]) % 256;
                temp = s[i];
                s[i] = s[j];
                s[j] = temp;
                var t = (s[i] + s[j]) % 256;
                data[x] ^= s[t];
            }
        }
    }
}