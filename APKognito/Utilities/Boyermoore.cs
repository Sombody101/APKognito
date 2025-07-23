namespace APKognito.Utilities;

public static class Grep
{
    //public static 
}

public static class Boyermoore
{
    public static int Index(string str, string subStr)
    {
        int[] table = CalculateSlideTable(subStr);
        return IndexWithTable(table, str, subStr);
    }

    public static int IndexWithTable(int[] table, string str, string subStr)
    {
        if (subStr.Length is 0)
        {
            return 0;
        }
        else if (subStr.Length > str.Length)
        {
            return -1;
        }
        else if (str.Length == subStr.Length)
        {
            return str == subStr ? 0 : -1;
        }

        int i = 0;
        while (i + subStr.Length - 1 < str.Length)
        {
            int j = subStr.Length - 1;
            for (; j >= 0 && str[i + j] == subStr[j]; --j) ;

            if (j < 0)
            {
                return i;
            }

            int slid = j - table[str[i + j]];
            if (slid < 1)
            {
                slid = 1;
            }

            i += slid;
        }

        return -1;
    }

    public static int[] CalculateSlideTable(string subStr)
    {
        int[] table = new int[256];

        for (int i = 0; i < 256; ++i)
        {
            table[i] = -1;
        }

        for (int i = 0; i < subStr.Length; ++i)
        {
            table[subStr[i]] = i;
        }

        return table;
    }
}
