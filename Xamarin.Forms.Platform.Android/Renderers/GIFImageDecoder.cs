using System.Threading.Tasks;
using System.Diagnostics;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;

namespace Xamarin.Forms.Platform.Android
{
	class GIFImageDecoder : Xamarin.Forms.Internals.GIFDecoder
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

		protected override Task<bool> AddBitmapAsync(int[] data, int width, int height, int delay)
		{
			Bitmap bitmap;
			bitmap = Bitmap.CreateBitmap(data, width, height, Bitmap.Config.Argb4444);

			if (_sourceDensity < _targetDensity)
			{
				var originalBitmap = bitmap;

				float scaleFactor = _targetDensity / _sourceDensity;

				int scaledWidth = (int)(scaleFactor * width);
				int scaledHeight = (int)(scaleFactor * height);
				bitmap = Bitmap.CreateScaledBitmap(originalBitmap, scaledWidth, scaledHeight, true);

				Debug.Assert(!originalBitmap.Equals(bitmap));

				originalBitmap.Recycle();
				originalBitmap.Dispose();
				originalBitmap = null;
			}

			// Frame delay compability adjustment in milliseconds.
			if (delay <= 20)
				delay = 100;

			_animation.AddFrame(new BitmapDrawable(_context.Resources, bitmap), delay);
			return Task.FromResult<bool>(true);
		}
	}
}