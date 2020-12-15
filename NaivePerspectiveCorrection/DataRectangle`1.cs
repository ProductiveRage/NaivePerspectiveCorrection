using System;
using System.Collections.Generic;
using System.Drawing;

namespace NaivePerspectiveCorrection
{
    internal sealed class DataRectangle<T>
    {
        private readonly T[,] _protectedValues;
        private readonly Rectangle _window;
        public DataRectangle(T[,] values) : this(values, window: null, isolationCopyMayBeBypassed: false) { }
        private DataRectangle(T[,] values, Rectangle? window, bool isolationCopyMayBeBypassed)
        {
            if ((values.GetLowerBound(0) != 0) || (values.GetLowerBound(1) != 0))
                throw new ArgumentException("Both dimensions must have lower bound zero");
            var arrayWidth = values.GetUpperBound(0) + 1;
            var arrayHeight = values.GetUpperBound(1) + 1;
            if ((arrayWidth == 0) || (arrayHeight == 0))
                throw new ArgumentException("zero element arrays are not supported");

            if (window.HasValue)
            {
                if ((window.Value.Left < 0) || (window.Value.Top < 0) || (window.Value.Right > arrayWidth) || (window.Value.Bottom > arrayHeight))
                    throw new ArgumentOutOfRangeException(nameof(window));
                Width = window.Value.Width;
                Height = window.Value.Height;
                _window = window.Value;
            }
            else
            {
                Width = arrayWidth;
                Height = arrayHeight;
                _window = new Rectangle(0, 0, arrayWidth, arrayHeight);
            }

            if (isolationCopyMayBeBypassed)
                _protectedValues = values;
            else
            {
                _protectedValues = new T[Width, Height];
                Array.Copy(values, _protectedValues, Width * Height);
            }
        }

        /// <summary>
        /// This will always be greater than zero
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// This will always be greater than zero
        /// </summary>
        public int Height { get; }

        public T this[int x, int y]
        {
            get
            {
                if ((x < 0) || (x >= Width))
                    throw new ArgumentOutOfRangeException(nameof(x));
                if ((y < 0) || (y >= Height))
                    throw new ArgumentOutOfRangeException(nameof(y));
                return _protectedValues[x + _window.Left, y + _window.Top];
            }
        }

        public TAccumulate Aggregate<TAccumulate>(TAccumulate seed, Func<TAccumulate, T, TAccumulate> func)
        {
            for (var x = _window.Left; x < _window.Right; x++)
            {
                for (var y = _window.Top; y < _window.Bottom; y++)
                    seed = func(seed, _protectedValues[x, y]);
            }
            return seed;
        }

        public IEnumerable<(Point Point, T Value)> Enumerate()
        {
            for (var x = _window.Left; x < _window.Right; x++)
            {
                for (var y = _window.Top; y < _window.Bottom; y++)
                {
                    var value = _protectedValues[x, y];
                    var point = new Point(x, y);
                    yield return (point, value);
                }
            }
        }

        public DataRectangle<TResult> Transform<TResult>(Func<T, TResult> transformer) => Transform((value, coordinates) => transformer(value));

        public DataRectangle<TResult> Transform<TResult>(Func<T, Point, TResult> transformer) => Transform((value, coordinates, source) => transformer(value, coordinates));

        public DataRectangle<TResult> Transform<TResult>(Func<T, Point, DataRectangle<T>, TResult> transformer)
        {
            var transformed = new TResult[Width, Height];
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                    transformed[x, y] = transformer(_protectedValues[_window.Left + x, _window.Top + y], new Point(x, y), this);
            }
            return new DataRectangle<TResult>(transformed, window: null, isolationCopyMayBeBypassed: true);
        }

        public DataRectangle<T> Slice(Rectangle bounds)
        {
            if ((bounds.Left < 0) || (bounds.Right > Width) || (bounds.Top < 0) || (bounds.Bottom > Height))
                throw new ArgumentOutOfRangeException(nameof(bounds));
            if ((bounds.Width <= 0) || (bounds.Height <= 0))
                throw new ArgumentException("zero element arrays are not supported", nameof(bounds));

            return new DataRectangle<T>(
                _protectedValues,
                window: new Rectangle(
                    _window.X + bounds.X,
                    _window.Y + bounds.Y,
                    bounds.Width,
                    bounds.Height
                ),
                isolationCopyMayBeBypassed: true
            );
        }

        public DataRectangle<TResult> BlockOut<TResult>(int blockSize, double blockSizeFractionToMove, Func<DataRectangle<T>, TResult> reducer)
        {
            if (blockSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(blockSize), "must be greater than zero");
            if ((blockSize > Width) || (blockSize > Height))
                throw new ArgumentOutOfRangeException(nameof(blockSize), "must not be larger than either Width nor Height");
            if ((blockSizeFractionToMove <= 0) || (blockSizeFractionToMove > 1))
                throw new ArgumentException(nameof(blockSizeFractionToMove), "must be greater than zero and no larger than one");

            // Use Math.Round so that if the source doesn't fit precisely into the required block size then it ignores the overflow if there isn't much of
            // it but includes it in additional blocks if there IS quite a lot
            var distanceToMoveEachTime = blockSize * blockSizeFractionToMove;
            var newWidth = (int)Math.Round((Width - (blockSize - distanceToMoveEachTime)) / distanceToMoveEachTime);
            var newHeight = (int)Math.Round((Height - (blockSize - distanceToMoveEachTime)) / distanceToMoveEachTime);
            var result = new TResult[newWidth, newHeight];
            for (var x = 0; x < newWidth; x++)
            {
                for (var y = 0; y < newHeight; y++)
                {
                    var left = (int)Math.Round(x * distanceToMoveEachTime);
                    var top = (int)Math.Round(y * distanceToMoveEachTime);
                    result[x, y] = reducer(Slice(
                        Rectangle.FromLTRB(
                            left: left,
                            top: top,
                            right: Math.Min(left + blockSize, Width),
                            bottom: Math.Min(top + blockSize, Height)
                        )
                    ));
                }
            }
            return new DataRectangle<TResult>(result);
        }
    }
}