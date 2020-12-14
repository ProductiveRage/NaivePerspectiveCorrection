namespace NaivePerspectiveCorrection
{
    internal static class DataRectangle
    {
        public static DataRectangle<T> For<T>(T[,] values) => new DataRectangle<T>(values);
    }
}