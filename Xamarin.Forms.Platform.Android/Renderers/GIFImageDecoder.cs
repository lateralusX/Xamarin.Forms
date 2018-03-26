using System.Threading.Tasks;
using System.Diagnostics;
using Xamarin.Forms.Internals;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;

namespace Xamarin.Forms.Platform.Android
{
	class GIFImageDecoder : GIFDecoder
	{
		readonly DisplayMetrics _metrics = Resources.System.DisplayMetrics;
		Context _context;
		int _sourceDensity;
		int _targetDensity;

		AnimationDrawable _animation = null;

		public GIFImageDecoder(Context context, int sourceDensity, int targetDensity)
		{
			_context = context;
			_animation = new AnimationDrawable();
			_sourceDensity = sourceDensity;
			_targetDensity = targetDensity;
		}

		public AnimationDrawable Animation { get { return _animation; } }

		protected override void StartParsing()
		{
			Debug.Assert(!_animation.IsRunning);
			Debug.Assert(_animation.NumberOfFrames == 0);
		}

		protected override void AddBitmap(GIFHeader header, GIFBitmap gifBitmap)
		{
			if (gifBitmap.Data != null)
			{
				Bitmap bitmap;
				bitmap = Bitmap.CreateBitmap(gifBitmap.Data, header.Width, header.Height, Bitmap.Config.Argb4444);

				if (_sourceDensity < _targetDensity)
				{
					var originalBitmap = bitmap;

					float scaleFactor = _targetDensity / _sourceDensity;

					int scaledWidth = (int)(scaleFactor * header.Width);
					int scaledHeight = (int)(scaleFactor * header.Height);
					bitmap = Bitmap.CreateScaledBitmap(originalBitmap, scaledWidth, scaledHeight, true);

					Debug.Assert(!originalBitmap.Equals(bitmap));

					originalBitmap.Recycle();
					originalBitmap.Dispose();
					originalBitmap = null;
				}

				// Frame delay compability adjustment in milliseconds.
				int delay = gifBitmap.Delay;
				if (delay <= 20)
					delay = 100;

				_animation.AddFrame(new BitmapDrawable(_context.Resources, bitmap), delay);
			}
		}

		protected override void FinishedParsing()
		{
			Debug.Assert(!_animation.IsRunning);
		}
	}
}