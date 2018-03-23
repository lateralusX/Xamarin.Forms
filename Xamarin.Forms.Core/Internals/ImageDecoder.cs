using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Xamarin.Forms.Internals
{
	public class GIFDecoderFormatException : Exception
	{
		public GIFDecoderFormatException()
			: base()
		{
			;
		}

		public GIFDecoderFormatException(string message)
			:base(message)
		{
			;
		}

		public GIFDecoderFormatException(string message, Exception innerException)
			: base(message, innerException)
		{
			;
		}
	}

	public class GIFDecoderStreamReader
	{
		Stream _stream;
		int _currentBlockSize;
		byte[] _blockBuffer = new byte[256];

		public GIFDecoderStreamReader(Stream stream)
		{
			_stream = stream;
		}
		
		public byte[] CurrentBlockBuffer {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get {
				return _blockBuffer;
			}
		}

		public int CurrentBlockSize {
			get {
				return _currentBlockSize;
			}
		}

		public int Read()
		{
			return _stream.ReadByte();
		}

		public int ReadShort()
		{
			return Read() | (Read() << 8);
		}

		public string ReadString(int length)
		{
			var buffer = new StringBuilder(length);
			for (int i = 0; i < length; i++)
				buffer.Append((char)_stream.ReadByte());
			return buffer.ToString();
		}

		public async Task<int> ReadAsync(byte[] buffer, int toRead)
		{
			int totalBytesRead = 0;
			if (toRead > 0)
			{
				Debug.Assert(toRead <= buffer.Length);

				int bytesRead = 0;
				while (totalBytesRead < toRead)
				{
					bytesRead = await _stream.ReadAsync(buffer, totalBytesRead, toRead - totalBytesRead).ConfigureAwait(false);
					if (bytesRead == -1)
					{
						break;
					}
					totalBytesRead += bytesRead;
				}
			}

			return totalBytesRead;
		}

		public async Task<int> ReadBlockAsync()
		{
			_currentBlockSize = Read();
			int bytesRead = await ReadAsync(_blockBuffer, _currentBlockSize).ConfigureAwait(false);

			if (bytesRead < _currentBlockSize)
			{
				throw new GIFDecoderFormatException("Current block to small.");
			}
			
			Debug.Assert(_currentBlockSize == bytesRead);
			return bytesRead;
		}

		public async Task SkipBlockAsync()
		{
			_currentBlockSize = Read();
			while (_currentBlockSize > 0)
			{
				if (_stream.CanSeek)
				{
					_stream.Seek(_currentBlockSize, SeekOrigin.Current);
				}
				else
				{
					await ReadAsync(_blockBuffer, _currentBlockSize).ConfigureAwait(false);
				}

				_currentBlockSize = Read();
			}
		}

		async Task SkipAsync()
		{
			await SkipBlockAsync().ConfigureAwait(false);
		}
	}

	public enum GIFDisposeMethod
	{
		NoAction = 0,
		LeaveInPlace = 1,
		RestoreToBackground = 2,
		RestoreToPrevious = 3
	};

	public class GIFBitmapRect
	{
		public GIFBitmapRect(int x, int y, int width, int height)
		{
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}

		public int X { get; }

		public int Y { get; }

		public int Width { get; }

		public int Height { get; }
	}

	public class GIFHeader
	{
		public string TypeIdentifier { get; private set; }

		public int Width { get; private set; }

		public int Height { get; private set; }

		public int BackgroundColorIndex { get; set; }

		public int BackgroundColor { get; set; }

		public GIFColorTable ColorTable { get; private set; }

		public int PixelAspectRatio { get; private set; }

		public bool IsGIFHeader
		{
			get {
				return !string.IsNullOrEmpty(TypeIdentifier) && TypeIdentifier.StartsWith("GIF", StringComparison.OrdinalIgnoreCase);
			}
		}

		public async Task ParseAsync(GIFDecoderStreamReader stream)
		{
			TypeIdentifier = stream.ReadString(6);
			if (IsGIFHeader)
			{
				Width = stream.ReadShort();
				Height = stream.ReadShort();
				await ParseGIFHeaderAsync(stream).ConfigureAwait(false);
			}
		}

		bool HasColorTable(int flags)
		{
			return ((flags & 0x80) != 0);
		}

		short ColorTableSize(int flags)
		{
			return (short)(2 << (flags & 7));
		}

		async Task ParseGIFHeaderAsync(GIFDecoderStreamReader stream)
		{
			int flags = stream.Read();
			if (HasColorTable(flags))
			{
				var table = new GIFColorTable(ColorTableSize(flags));
				await table.ParseAsync(stream).ConfigureAwait(false);
				ColorTable = table;
			}

			BackgroundColorIndex = stream.Read();
			if (ColorTable != null)
				BackgroundColor = ColorTable.Color(BackgroundColorIndex);

			PixelAspectRatio = stream.Read();
		}
	}

	public class GIFColorTable
	{
		int[] _colorTable = new int[256];
		byte[] _colorData = null;
		short _size = 0;
		
		public GIFColorTable(short size)
		{
			_colorData = new byte[3 * size];
			_size = size;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Color(int index)
		{
			return _colorTable[index];
		}

		public async Task ParseAsync(GIFDecoderStreamReader stream)
		{
			int toRead = _colorData.Length;
			int bytesRead = await stream.ReadAsync(_colorData, toRead).ConfigureAwait(false);
			if (bytesRead < toRead)
				throw new GIFDecoderFormatException("Invalid color data table size.");
			
			int i = 0;
			int j = 0;
			while (i < _size)
			{
				int r = _colorData[j++] & 0xFF;
				int g = _colorData[j++] & 0xFF;
				int b = _colorData[j++] & 0xFF;

				var rgb = (r << 16) | (g << 8) | b;
				_colorTable[i++] = (int)(0xFF000000 | rgb);
			}
		}

		int _transparencyIndex = -1;
		int _oldColorValue = -1;

		public void SetColors(GIFHeader header, GIFBitmap bitmap)
		{
			if (bitmap.HasTrancparency)
			{
				_oldColorValue = _colorData[bitmap.TransparencyIndex];
				_transparencyIndex = bitmap.TransparencyIndex;
			}
		}

		public void ResetColors()
		{
			if (_transparencyIndex != -1 && _oldColorValue != -1)
			{
				_colorData[_transparencyIndex] = (byte)_oldColorValue;
				_transparencyIndex = _oldColorValue = -1;
			}
		}
	}

	public class GIFBitmap
	{
		public GIFBitmap(int[] data, GIFBitmapRect bounds, GIFDisposeMethod disposeMethod, int backgroundColor, bool hasTransparency, bool isInterlaced, GIFColorTable colorTable)
		{
			Data = data;
			Bounds = bounds;
			DisposeMethod = disposeMethod;
			BackgroundColor = backgroundColor;
			HasTrancparency = hasTransparency;
			IsInterlaced = isInterlaced;
			ColorTable = colorTable;
		}

		GIFBitmap()
		{
			DisposeMethod = GIFDisposeMethod.NoAction;
		}

		public int[] Data { get; set; }

		public GIFBitmapRect Bounds { get; private set; }

		public GIFDisposeMethod DisposeMethod { get; private set; }

		public int BackgroundColor { get; private set; }

		public bool HasTrancparency { get; private set; }

		public int TransparencyIndex { get; private set; }

		public bool IsInterlaced { get; private set; }

		public int Delay { get; private set; }

		public int LoopCount { get; private set; }

		public GIFColorTable ColorTable { get; private set; }

		void SetDisposeMethod(int flags)
		{
			DisposeMethod = (GIFDisposeMethod)((flags & 0x1C) >> 2);
			if (DisposeMethod == GIFDisposeMethod.NoAction)
				DisposeMethod = GIFDisposeMethod.LeaveInPlace;
		}

		void SetTransparency(int flags)
		{
			HasTrancparency = (flags & 1) != 0;
		}

		void SetDelay(int delay)
		{
			// Convert to milliseconds.
			Delay = Math.Max(10, delay * 10);
		}

		void SetTransparencyIndex(int index)
		{
			TransparencyIndex = index;
		}

		bool UseLocalColorTable(int flags)
		{
			return ((flags & 0x80) != 0);
		}

		short LocalColorTableSize(int flags)
		{
			return (short)(Math.Pow(2, (flags & 0x07) + 1));
		}

		bool UseInterlace(int flags)
		{
			return ((flags & 0x40) != 0);
		}

		public void ParseGraphicControlExtension(GIFDecoderStreamReader stream)
		{
			int size = stream.Read();
			Debug.Assert(size != 0);

			int flags = stream.Read();
			SetDisposeMethod(flags);
			SetTransparency(flags);

			SetDelay(stream.ReadShort());
			TransparencyIndex = stream.Read();

			int blockTerminator = stream.Read();
			Debug.Assert(blockTerminator == 0);
		}

		public async Task ParseNetscapeApplicationExtensionAsync(GIFDecoderStreamReader stream)
		{
			int blockSize = await stream.ReadBlockAsync().ConfigureAwait(false);
			while (blockSize > 0)
			{
				if (stream.CurrentBlockBuffer[0] == 1)
				{
					Debug.Assert(blockSize >= 3);
					LoopCount = (stream.CurrentBlockBuffer[2] << 8) | stream.CurrentBlockBuffer[1];
				}
				blockSize = await stream.ReadBlockAsync().ConfigureAwait(false);
			}
		}

		public async Task ParseApplicationExtensionAsync(GIFDecoderStreamReader stream)
		{
			var blockSize = await stream.ReadBlockAsync().ConfigureAwait(false);
			if (blockSize >= 11)
			{
				var buffer = stream.CurrentBlockBuffer;
				string identifier = System.Text.Encoding.UTF8.GetString(buffer, 0, 11);
				if (identifier.Equals("NETSCAPE2.0", StringComparison.OrdinalIgnoreCase))
				{
					await ParseNetscapeApplicationExtensionAsync(stream).ConfigureAwait(false);
					return;
				}
			}
			await stream.SkipBlockAsync().ConfigureAwait(false);
			return;
		}

		public void ParseGIFBitmapHeader(GIFDecoderStreamReader stream, GIFHeader header)
		{
			Bounds = new GIFBitmapRect(stream.ReadShort(), stream.ReadShort(), stream.ReadShort(), stream.ReadShort());
			
			int flags = stream.Read();
			bool localTable = UseLocalColorTable(flags);

			ColorTable = header.ColorTable;

			if (localTable)
			{
				ColorTable = new GIFColorTable(LocalColorTableSize(flags));
			}
			else if (!localTable && TransparencyIndex == header.BackgroundColorIndex)
			{
				// Reset global background color. NOTE, this change will be applied to all future bitmaps.
				header.BackgroundColor = 0;
			}

			BackgroundColor = header.BackgroundColor;
			IsInterlaced = UseInterlace(flags);
		}

		public async Task ParseImageDescriptorAsync(GIFDecoderStreamReader stream, GIFHeader header, GIFBitmapDecoder decoder, GIFBitmap previousBitmap)
		{
			ParseGIFBitmapHeader(stream, header);
			ColorTable.SetColors(header, this);

			await decoder.DecodeBitmapDataAsync(stream, header.Width, header.Height).ConfigureAwait(false);
			await stream.SkipBlockAsync().ConfigureAwait(false);
			decoder.ComposeGIFBitmap(header, this, previousBitmap);

			ColorTable.ResetColors();
		}

		public async Task ParseExtensionAsync(GIFDecoderStreamReader stream)
		{
			int blockCode = stream.Read();
			switch (blockCode)
			{
				case GIFBlockCodes.GraphicsControlExtension:
					ParseGraphicControlExtension(stream);
					break;
				case GIFBlockCodes.ApplicationExtensionBlock:
					await ParseApplicationExtensionAsync(stream).ConfigureAwait(false);
					break;
				default:
					await stream.SkipBlockAsync().ConfigureAwait(false);
					break;
			}
		}

		class GIFBlockCodes
		{
			public const int ImageSeparator = 0x2C;
			public const int Extension = 0x21;
			public const int Trailer = 0x3B;
			public const int GraphicsControlExtension = 0xF9;
			public const int ApplicationExtensionBlock = 0xFF;
		}

		public static async Task<GIFBitmap> ParseBitmapAsync(GIFDecoderStreamReader stream, GIFHeader header, GIFBitmapDecoder decoder, GIFBitmap previousBitmap)
		{
			GIFBitmap currentBitmap = null;
			bool done = false;
			
			while (!done)
			{
				int blockCode = stream.Read();
				switch (blockCode)
				{
					case GIFBlockCodes.ImageSeparator:
						if (currentBitmap == null)
							currentBitmap = new GIFBitmap();
						await currentBitmap.ParseImageDescriptorAsync(stream, header, decoder, previousBitmap).ConfigureAwait(false);
						done = true;
						break;
					case GIFBlockCodes.Extension:
						if (currentBitmap == null)
							currentBitmap = new GIFBitmap();
						await currentBitmap.ParseExtensionAsync(stream).ConfigureAwait(false);
						break;
					case GIFBlockCodes.Trailer:
						done = true;
						break;
					default:
						break;
				}
			}

			return currentBitmap;
		}
	}

	public class GIFBitmapDecoder
	{
		short[] _prefix;
		byte[] _suffix;
		byte[] _pixelStack;
		byte[] _pixels;

		const int DecoderStackSize = 4096;

		void Initialize(int pixelCount)
		{ 
			if (_pixels == null || _pixels.Length < pixelCount)
			{
				_pixels = new byte[pixelCount];
			}
			if (_prefix == null)
			{
				_prefix = new short[DecoderStackSize];
			}
			if (_suffix == null)
			{
				_suffix = new byte[DecoderStackSize];
			}
			if (_pixelStack == null)
			{
				_pixelStack = new byte[DecoderStackSize + 1];
			}
		}

		public void ComposeGIFBitmap(GIFHeader header, GIFBitmap currentBitmap, GIFBitmap previousBitmap)
		{
			int[] result = null;
			int width = header.Width;
			int height = header.Height;
			GIFBitmapRect bounds = currentBitmap.Bounds;
			GIFDisposeMethod disposeMethod = currentBitmap.DisposeMethod;
			int backgroundColor = currentBitmap.BackgroundColor;
			bool useTransparency = currentBitmap.HasTrancparency;
			bool useInterlace = currentBitmap.IsInterlaced;
			GIFColorTable colorTable = currentBitmap.ColorTable;

			if (previousBitmap != null && previousBitmap.DisposeMethod != GIFDisposeMethod.NoAction)
			{
				if (previousBitmap.DisposeMethod == GIFDisposeMethod.RestoreToPrevious)
				{
					Debug.Assert(previousBitmap.Data != null);
				}

				if (previousBitmap.Data != null)
				{
					result = previousBitmap.Data;
					if (previousBitmap.DisposeMethod == GIFDisposeMethod.RestoreToBackground)
					{ 
						int color = 0;
						if (!useTransparency)
						{
							color = previousBitmap.BackgroundColor;
						}

						var previousBitmapBounds = previousBitmap.Bounds;
						for (int i = 0; i < previousBitmapBounds.Height; i++)
						{
							int n1 = (previousBitmapBounds.Y + i) * width + previousBitmapBounds.X;
							int n2 = n1 + previousBitmapBounds.Width;
							for (int k = n1; k < n2; k++)
							{
								result[k] = color;
							}
						}
					}
				}
			}

			if (result == null)
			{
				result = new int[width * height];
			}

			int pass = 1;
			int inc = 8;
			int iline = 0;
			for (int i = 0; i < bounds.Height; i++)
			{
				int line = i;
				if (useInterlace)
				{
					if (iline >= bounds.Height)
					{
						pass++;
						switch (pass)
						{
							case 2:
								iline = 4;
								break;
							case 3:
								iline = 2;
								inc = 4;
								break;
							case 4:
								iline = 1;
								inc = 2;
								break;
							default:
								break;
						}
					}
					line = iline;
					iline += inc;
				}
				line += bounds.Y;
				if (line < height)
				{
					int k = line * width;
					int dx = k + bounds.X; // start of line in dest
					int dlim = dx + bounds.Width; // end of dest line
					if ((k + width) < dlim)
					{
						dlim = k + width; // past dest edge
					}
					int sx = i * bounds.Width; // start of line in source
					while (dx < dlim)
					{
						// map color and insert in destination
						int index = ((int)_pixels[sx++]) & 0xff;
						int c = colorTable.Color(index);
						if (c != 0)
						{
							result[dx] = c;
						}
						dx++;
					}
				}
			}

			currentBitmap.Data = result;
		}

		public async Task DecodeBitmapDataAsync(GIFDecoderStreamReader stream, int width, int height)
		{
			int nullCode = -1;
			int npix = width * height;
			int available, clear, code_mask, code_size, end_of_information, in_code, old_code, bits, code, count, i, datum, data_size, first, top, bi, pi;
			
			Initialize(npix);

			data_size = stream.Read();
			clear = 1 << data_size;
			end_of_information = clear + 1;
			available = clear + 2;
			old_code = nullCode;
			code_size = data_size + 1;
			code_mask = (1 << code_size) - 1;
			for (code = 0; code < clear; code++)
			{
				_prefix[code] = 0;
				_suffix[code] = (byte)code;
			}
			// Decode GIF pixel stream.
			datum = bits = count = first = top = pi = bi = 0;
			for (i = 0; i < npix;)
			{
				if (top == 0)
				{
					if (bits < code_size)
					{
						// Load bytes until there are enough bits for a code.
						if (count == 0)
						{
							// Read a new data block.
							count = await stream.ReadBlockAsync().ConfigureAwait(false);
							if (count <= 0)
							{
								break;
							}
							bi = 0;
						}
						datum += (stream.CurrentBlockBuffer[bi] & 0xFF) << bits;
						bits += 8;
						bi++;
						count--;
						continue;
					}
					// Get the next code.
					code = datum & code_mask;
					datum >>= code_size;
					bits -= code_size;
					// Interpret the code
					if ((code > available) || (code == end_of_information))
					{
						break;
					}
					if (code == clear)
					{
						// Reset decoder.
						code_size = data_size + 1;
						code_mask = (1 << code_size) - 1;
						available = clear + 2;
						old_code = nullCode;
						continue;
					}
					if (old_code == nullCode)
					{
						_pixelStack[top++] = _suffix[code];
						old_code = code;
						first = code;
						continue;
					}
					in_code = code;
					if (code == available)
					{
						_pixelStack[top++] = (byte)first;
						code = old_code;
					}
					while (code > clear)
					{
						_pixelStack[top++] = _suffix[code];
						code = _prefix[code];
					}
					first = ((int)_suffix[code]) & 0xff;
					// Add a new string to the string table,
					if (available >= DecoderStackSize)
					{
						break;
					}
					_pixelStack[top++] = (byte)first;
					_prefix[available] = (short)old_code;
					_suffix[available] = (byte)first;
					available++;
					if (((available & code_mask) == 0) && (available < DecoderStackSize))
					{
						code_size++;
						code_mask += available;
					}
					old_code = in_code;
				}
				// Pop a pixel off the pixel stack.
				top--;
				_pixels[pi++] = _pixelStack[top];
				i++;
			}
			for (i = pi; i < npix; i++)
			{
				_pixels[i] = 0; // clear missing pixels
			}
		}
	}


	public abstract class GIFDecoder
	{
		const int STATUS_OK = 0;
		const int STATUS_FORMAT_ERROR = 1;
		const int STATUS_OPEN_ERROR = 2;
		int status;
		const int MAX_STACK_SIZE = 4096;
		Stream input;
		int width;
		int height;
		bool gctFlag; // global color table used
		int gctSize; // size of global color table
		int loopCount = 1; // iterations; 0 = repeat forever
		int[] gct; // global color table
		int[] lct; // local color table
		int[] act; // active color table
		int bgIndex; // background color index
		int bgColor; // background color
		int lastBgColor; // previous bg color
		int pixelAspect; // pixel aspect ratio
		bool lctFlag; // local color table flag
		bool interlace; // interlace flag
		int lctSize; // local color table size
		int ix, iy, iw, ih; // current image rectangle
		int lrx, lry, lrw, lrh;
		//TNativeImage image; // current frame
		int[] currentBitmap;
		int[] lastBitmap1; // previous frame
		byte[] block = new byte[256]; // current data block
		int blockSize = 0; // block size last graphic control extension info
		int dispose = 0; // 0=no action; 1=leave in place; 2=restore to bg; 3=restore to prev
		int lastDispose = 0;
		bool transparency = false; // use transparent color
		int delay = 0; // delay in milliseconds
		int transIndex; // transparent color index
						// LZW decoder working arrays
		short[] prefix;
		byte[] suffix;
		byte[] pixelStack;
		byte[] pixels;
		int frameCount;

		GIFBitmapDecoder _bitmapDecoder = new GIFBitmapDecoder();
		GIFBitmap _previousGIFBitmap = null;
		GIFBitmap _currentGIFBitmap = null;
		GIFDecoderStreamReader _streamReader = null;
		GIFHeader _gifHeader = null;
		
		protected abstract Task<bool> AddBitmapAsync(int[] data, int width, int height, int delay);



		public async Task ParseGIFAsync(Stream inputStream)
		{
			if (inputStream != null)
			{
				GIFBitmapDecoder bitmapDecoder = new GIFBitmapDecoder();
				GIFBitmap previousGIFBitmap = null;
				GIFBitmap currentGIFBitmap = null;
				GIFDecoderStreamReader streamReader = new GIFDecoderStreamReader(inputStream);
				GIFHeader gifHeader = new GIFHeader();

				await gifHeader.ParseAsync(streamReader).ConfigureAwait(false);
				currentGIFBitmap = await GIFBitmap.ParseBitmapAsync(streamReader, gifHeader, bitmapDecoder, previousGIFBitmap).ConfigureAwait(false);
				while (currentGIFBitmap != null && currentGIFBitmap.Data != null)
				{
					await AddBitmapAsync(currentGIFBitmap.Data, gifHeader.Width, gifHeader.Height, currentGIFBitmap.Delay);
					previousGIFBitmap = currentGIFBitmap;
					currentGIFBitmap = await GIFBitmap.ParseBitmapAsync(streamReader, gifHeader, bitmapDecoder, previousGIFBitmap).ConfigureAwait(false);
				}
			}
			else
			{
				throw new ArgumentNullException(nameof(inputStream));
			}
		}



		async Task NewSetPixelsAsync()
		{
			//_currentGIFBitmap = new GIFBitmap(null, new GIFBitmapRect(ix, iy, iw, ih), (GIFDisposeMethod)dispose, bgColor, transparency, interlace, new GIFColorTable( act);
			_bitmapDecoder.ComposeGIFBitmap(_gifHeader,_currentGIFBitmap, _previousGIFBitmap);
			await AddBitmapAsync(_currentGIFBitmap.Data, width, height, delay).ConfigureAwait(false);
		}

		async Task SetPixelsAsync()
		{
			int[] result = new int[width * height];

			// fill in starting image contents based on last image's dispose code
			if (lastDispose > 0)
			{
				if (lastDispose == 3)
				{
					// use image before last
					int n = frameCount - 2;
					if (n > 0)
					{
						lastBitmap1 = currentBitmap;
					}
					else
					{
						lastBitmap1 = null;
					}
				}
				if (currentBitmap != null)
				{
					result = lastBitmap1;

					// copy pixels
					if (lastDispose == 2)
					{
						// fill last image rect area with background color
						int c = 0;
						if (!transparency)
						{
							c = lastBgColor;
						}
						for (int i = 0; i < lrh; i++)
						{
							int n1 = (lry + i) * width + lrx;
							int n2 = n1 + lrw;
							for (int k = n1; k < n2; k++)
							{
								result[k] = c;
							}
						}
					}
				}
			}
			// copy each source line to the appropriate place in the destination
			int pass = 1;
			int inc = 8;
			int iline = 0;
			for (int i = 0; i < ih; i++)
			{
				int line = i;
				if (interlace)
				{
					if (iline >= ih)
					{
						pass++;
						switch (pass)
						{
							case 2:
								iline = 4;
								break;
							case 3:
								iline = 2;
								inc = 4;
								break;
							case 4:
								iline = 1;
								inc = 2;
								break;
							default:
								break;
						}
					}
					line = iline;
					iline += inc;
				}
				line += iy;
				if (line < height)
				{
					int k = line * width;
					int dx = k + ix; // start of line in dest
					int dlim = dx + iw; // end of dest line
					if ((k + width) < dlim)
					{
						dlim = k + width; // past dest edge
					}
					int sx = i * iw; // start of line in source
					while (dx < dlim)
					{
						// map color and insert in destination
						int index = ((int)pixels[sx++]) & 0xff;
						int c = act[index];
						if (c != 0)
						{
							result[dx] = c;
						}
						dx++;
					}
				}
			}

			currentBitmap = result;

			await AddBitmapAsync(result, width, height, delay);
		}

		public async Task ReadGifAsync(Stream inputStream)
		{
			if (inputStream != null)
			{
				input = inputStream;
				_streamReader = new GIFDecoderStreamReader(inputStream);
				await ReadHeaderAsync().ConfigureAwait(false);
				if (!Err)
				{
					await ReadContentsAsync().ConfigureAwait(false);
					if (frameCount < 0)
					{
						throw new Exception("GIF parsing error");
					}
				}
			}
			else
			{
				throw new ArgumentNullException(nameof(inputStream));
			}

			if (status != STATUS_OK)
				throw new Exception("GIF parsing error");
		}

		async Task NewDecodeBitmapDataAsync()
		{
			await _bitmapDecoder.DecodeBitmapDataAsync(_streamReader, iw, ih).ConfigureAwait(false);
		}

		async Task DecodeBitmapDataAsync()
		{
			int nullCode = -1;
			int npix = iw * ih;
			int available, clear, code_mask, code_size, end_of_information, in_code, old_code, bits, code, count, i, datum, data_size, first, top, bi, pi;
			if ((pixels == null) || (pixels.Length < npix))
			{
				pixels = new byte[npix]; // allocate new pixel array
			}
			if (prefix == null)
			{
				prefix = new short[MAX_STACK_SIZE];
			}
			if (suffix == null)
			{
				suffix = new byte[MAX_STACK_SIZE];
			}
			if (pixelStack == null)
			{
				pixelStack = new byte[MAX_STACK_SIZE + 1];
			}

			// Initialize GIF data stream decoder.
			data_size = Read();
			clear = 1 << data_size;
			end_of_information = clear + 1;
			available = clear + 2;
			old_code = nullCode;
			code_size = data_size + 1;
			code_mask = (1 << code_size) - 1;
			for (code = 0; code < clear; code++)
			{
				prefix[code] = 0; // XXX ArrayIndexOutOfBoundsException
				suffix[code] = (byte)code;
			}
			// Decode GIF pixel stream.
			datum = bits = count = first = top = pi = bi = 0;
			for (i = 0; i < npix;)
			{
				if (top == 0)
				{
					if (bits < code_size)
					{
						// Load bytes until there are enough bits for a code.
						if (count == 0)
						{
							// Read a new data block.
							count = await ReadBlockAsync().ConfigureAwait(false);
							if (count <= 0)
							{
								break;
							}
							bi = 0;
						}
						datum += (((int)block[bi]) & 0xff) << bits;
						bits += 8;
						bi++;
						count--;
						continue;
					}
					// Get the next code.
					code = datum & code_mask;
					datum >>= code_size;
					bits -= code_size;
					// Interpret the code
					if ((code > available) || (code == end_of_information))
					{
						break;
					}
					if (code == clear)
					{
						// Reset decoder.
						code_size = data_size + 1;
						code_mask = (1 << code_size) - 1;
						available = clear + 2;
						old_code = nullCode;
						continue;
					}
					if (old_code == nullCode)
					{
						pixelStack[top++] = suffix[code];
						old_code = code;
						first = code;
						continue;
					}
					in_code = code;
					if (code == available)
					{
						pixelStack[top++] = (byte)first;
						code = old_code;
					}
					while (code > clear)
					{
						pixelStack[top++] = suffix[code];
						code = prefix[code];
					}
					first = ((int)suffix[code]) & 0xff;
					// Add a new string to the string table,
					if (available >= MAX_STACK_SIZE)
					{
						break;
					}
					pixelStack[top++] = (byte)first;
					prefix[available] = (short)old_code;
					suffix[available] = (byte)first;
					available++;
					if (((available & code_mask) == 0) && (available < MAX_STACK_SIZE))
					{
						code_size++;
						code_mask += available;
					}
					old_code = in_code;
				}
				// Pop a pixel off the pixel stack.
				top--;
				pixels[pi++] = pixelStack[top];
				i++;
			}
			for (i = pi; i < npix; i++)
			{
				pixels[i] = 0; // clear missing pixels
			}
		}
		
		bool Err => status != STATUS_OK;

		int Read()
		{
			int curByte = 0;
			try
			{
				curByte = input.ReadByte();
			}
			catch (Exception)
			{
				status = STATUS_FORMAT_ERROR;
			}
			return curByte;
		}

		async Task<int> ReadBlockAsync()
		{
			blockSize = Read();
			int n = 0;
			if (blockSize > 0)
			{
				try
				{
					int count = 0;
					while (n < blockSize)
					{
						count = await input.ReadAsync(block, n, blockSize - n);
						if (count == -1)
						{
							break;
						}
						n += count;
					}
				}
				catch (Exception e)
				{
					System.Diagnostics.Debug.WriteLine(e.ToString());
				}
				if (n < blockSize)
				{
					status = STATUS_FORMAT_ERROR;
				}
			}
			return n;
		}

		async Task<int[]> ReadColorTableAsync(int ncolors)
		{
			int nbytes = 3 * ncolors;
			int[] tab = null;
			byte[] c = new byte[nbytes];
			int n = 0;
			try
			{
				n = await input.ReadAsync(c, 0, c.Length);
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(e.ToString());
			}
			if (n < nbytes)
			{
				status = STATUS_FORMAT_ERROR;
			}
			else
			{
				tab = new int[256]; // max size to avoid bounds checks
				int i = 0;
				int j = 0;
				while (i < ncolors)
				{
					int r = ((int)c[j++]) & 0xff;
					int g = ((int)c[j++]) & 0xff;
					int b = ((int)c[j++]) & 0xff;
					var rgb = (r << 16) | (g << 8) | b;
					tab[i++] = (int)(0xff000000 | rgb);
				}
			}
			return tab;
		}

		async Task ReadContentsAsync()
		{
			// read GIF file content blocks
			bool done = false;
			while (!(done || Err))
			{
				int code = Read();
				switch (code)
				{
					case 0x2C: // image separator
						await ReadBitmapAsync().ConfigureAwait(false);
						break;
					case 0x21: // extension
						code = Read();
						switch (code)
						{
							case 0xf9: // graphics control extension
								ReadGraphicControlExt();
								break;
							case 0xff: // application extension
								await ReadBlockAsync().ConfigureAwait(false);
								String app = "";
								for (int i = 0; i < 11; i++)
								{
									app += (char)block[i];
								}
								if (app.Equals("NETSCAPE2.0", StringComparison.OrdinalIgnoreCase))
								{
									await ReadNetscapeExtAsync().ConfigureAwait(false);
								}
								else
								{
									await SkipAsync().ConfigureAwait(false); // don't care
								}
								break;
							case 0xfe:// comment extension
								await SkipAsync().ConfigureAwait(false);
								break;
							case 0x01:// plain text extension
								await SkipAsync().ConfigureAwait(false);
								break;
							default: // uninteresting extension
								await SkipAsync().ConfigureAwait(false);
								break;
						}
						break;
					case 0x3b: // terminator
						done = true;
						break;
					case 0x00: // bad byte, but keep going and see what happens break;
					default:
						status = STATUS_FORMAT_ERROR;
						break;
				}
			}
		}

		void ReadGraphicControlExt()
		{
			Read(); // block size
			int packed = Read(); // packed fields
			dispose = (packed & 0x1c) >> 2; // disposal method
			if (dispose == 0)
			{
				dispose = 1; // elect to keep old image if discretionary
			}
			transparency = (packed & 1) != 0;
			delay = Math.Max(10, ReadShort() * 10); // delay in milliseconds, enforcing min 10ms framerate

			transIndex = Read(); // transparent color index
			Read(); // block terminator
		}

		async Task NewReadHeaderAsync()
		{
			_gifHeader = new GIFHeader();
			await _gifHeader.ParseAsync(_streamReader).ConfigureAwait(false);
		}

		async Task ReadHeaderAsync()
		{
			String id = "";
			for (int i = 0; i < 6; i++)
			{
				id += (char)Read();
			}
			if (!id.StartsWith("GIF", StringComparison.OrdinalIgnoreCase))
			{
				status = STATUS_FORMAT_ERROR;
				return;
			}
			ReadLSD();
			if (gctFlag && !Err)
			{
				gct = await ReadColorTableAsync(gctSize);
				bgColor = gct[bgIndex];
			}
		}

		async Task ReadBitmapAsync()
		{
			ix = ReadShort(); // (sub)image position & size
			iy = ReadShort();
			iw = ReadShort();
			ih = ReadShort();
			int packed = Read();
			lctFlag = (packed & 0x80) != 0; // 1 - local color table flag interlace
			lctSize = (int)Math.Pow(2, (packed & 0x07) + 1);
			// 3 - sort flag
			// 4-5 - reserved lctSize = 2 << (packed & 7); // 6-8 - local color
			// table size
			interlace = (packed & 0x40) != 0;
			if (lctFlag)
			{
				lct = await ReadColorTableAsync(lctSize); // read table
				act = lct; // make local table active
			}
			else
			{
				act = gct; // make global table active
				if (bgIndex == transIndex)
				{
					bgColor = 0;
				}
			}
			int save = 0;
			if (transparency)
			{
				save = act[transIndex];
				act[transIndex] = 0; // set transparent color if specified
			}
			if (act == null)
			{
				status = STATUS_FORMAT_ERROR; // no color table defined
			}
			if (Err)
			{
				return;
			}
			//await DecodeBitmapDataAsync().ConfigureAwait(false); // decode pixel data
			await NewDecodeBitmapDataAsync().ConfigureAwait(false); // decode pixel data
			await SkipAsync().ConfigureAwait(false);
			if (Err)
			{
				return;
			}
			frameCount++;
			//await SetPixelsAsync().ConfigureAwait(false); // transfer pixel data to image
			await NewSetPixelsAsync().ConfigureAwait(false); // transfer pixel data to image
			if (transparency)
			{
				act[transIndex] = save;
			}

			ResetFrame();
		}

		void ReadLSD()
		{
			// logical screen size
			width = ReadShort();
			height = ReadShort();

			// packed fields
			int packed = Read();
			gctFlag = (packed & 0x80) != 0; // 1 : global color table flag
											// 2-4 : color resolution
											// 5 : gct sort flag
			gctSize = 2 << (packed & 7); // 6-8 : gct size
			bgIndex = Read(); // background color index
			pixelAspect = Read(); // pixel aspect ratio
		}

		async Task ReadNetscapeExtAsync()
		{
			do
			{
				await ReadBlockAsync().ConfigureAwait(false);
				if (block[0] == 1)
				{
					// loop count sub-block
					int b1 = ((int)block[1]) & 0xff;
					int b2 = ((int)block[2]) & 0xff;
					loopCount = (b2 << 8) | b1;
				}
			} while ((blockSize > 0) && !Err);
		}

		int ReadShort()
		{
			// read 16-bit value, LSB first
			return Read() | (Read() << 8);
		}

		void ResetFrame()
		{
			lastDispose = dispose;
			lrx = ix;
			lry = iy;
			lrw = iw;
			lrh = ih;
			lastBitmap1 = currentBitmap;
			_previousGIFBitmap = _currentGIFBitmap;
			lastBgColor = bgColor;
			dispose = 0;
			transparency = false;
			delay = 0;
			lct = null;
		}

		void SeekBlock()
		{
			Debug.Assert(input.CanSeek);

			blockSize = Read();
			if (blockSize != 0)
				input.Seek(blockSize, SeekOrigin.Current);
		}

		async Task SkipAsync()
		{
			do
			{
				if (input.CanSeek)
					SeekBlock();
				else
					await ReadBlockAsync().ConfigureAwait(false);
			} while ((blockSize > 0) && !Err);
		}
	}

}
