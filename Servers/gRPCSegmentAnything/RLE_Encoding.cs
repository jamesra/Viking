namespace gRPCSegmentAnything;

public class RLE_Encoding
{
    public static List<(int Label, int Count)> RunLengthEncode(int[,] labeledImage)
    {
        var encoded = new List<(int Label, int Count)>();
        int rows = labeledImage.GetLength(0);
        int cols = labeledImage.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            int currentLabel = labeledImage[i, 0];
            int count = 1;

            for (int j = 1; j < cols; j++)
            {
                if (labeledImage[i, j] == currentLabel)
                {
                    count++;
                }
                else
                {
                    encoded.Add((currentLabel, count));
                    currentLabel = labeledImage[i, j];
                    count = 1;
                }
            }

            // Add the last run in the row
            encoded.Add((currentLabel, count));
        }

        return encoded;
    }

    public static int[,] RunLengthDecode(List<(int Label, int Count)> encoded, int rows, int cols)
    {
        var decoded = new int[rows, cols];
        int row = 0, col = 0;

        foreach (var (label, count) in encoded)
        {
            for (int i = 0; i < count; i++)
            {
                decoded[row, col] = label;
                col++;

                if (col == cols)
                {
                    col = 0;
                    row++;
                }
            }
        }

        return decoded;
    }


}