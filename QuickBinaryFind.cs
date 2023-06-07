namespace JarInfectionScanner {
  public class QuickBinaryFind {
    public static int FindByteSubstring(byte[] largerArray, byte[] substring) {
      int[] prefixTable = BuildPrefixTable(substring);
      int i = 0;  // index for largerArray
      int j = 0;  // index for substring

      while (i < largerArray.Length) {
        if (largerArray[i] == substring[j]) {
          i++;
          j++;

          if (j == substring.Length) {
            // Substring found
            return i - j;
          }
        } else if (j > 0) {
          j = prefixTable[j - 1];
        } else {
          i++;
        }
      }

      // Substring not found
      return -1;
    }

    private static int[] BuildPrefixTable(byte[] substring) {
      int[] prefixTable = new int[substring.Length];
      int length = 0;
      int i = 1;

      while (i < substring.Length) {
        if (substring[i] == substring[length]) {
          length++;
          prefixTable[i] = length;
          i++;
        } else {
          if (length != 0) {
            length = prefixTable[length - 1];
          } else {
            prefixTable[i] = 0;
            i++;
          }
        }
      }

      return prefixTable;
    }
  }
}
