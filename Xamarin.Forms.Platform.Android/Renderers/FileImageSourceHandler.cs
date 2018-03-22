using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Android;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Xamarin.Forms.Internals;

namespace Xamarin.Forms.Platform.Android
{

	class GIFDecoder : Xamarin.Forms.Core.Internals.GIFDecoder
	{
		readonly DisplayMetrics _metrics = Resources.System.DisplayMetrics;
		Context _context;
		int _sourceDensity;
		int _targetDensity;

		AnimationDrawable _animation = null;

		public GIFDecoder(Context context, int sourceDensity, int targetDensity)
		{
			_context = context;
			_animation = new AnimationDrawable();
			_sourceDensity = sourceDensity;
			_targetDensity = targetDensity;
		}

		public AnimationDrawable Animation { get { return _animation; } }

		protected override Task<bool> AddBitmapAsync(int[] data, int width, int height, int duration)
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
			if (duration <= 20)
				duration = 100;

			_animation.AddFrame(new BitmapDrawable(_context.Resources, bitmap), duration);
			return Task.FromResult<bool>(true);
		}
	}

	public sealed class FileImageSourceHandler : IImageSourceHandlerEx
	{
		// This is set to true when run under designer context
		internal static bool DecodeSynchronously {
			get;
			set;
		}

		public async Task<Bitmap> LoadImageAsync(ImageSource imagesource, Context context, CancellationToken cancelationToken = default(CancellationToken))
		{
			string file = ((FileImageSource)imagesource).File;
			Bitmap bitmap;
			if (File.Exists (file))
				bitmap = !DecodeSynchronously ? (await BitmapFactory.DecodeFileAsync (file).ConfigureAwait (false)) : BitmapFactory.DecodeFile (file);
			else
				bitmap = !DecodeSynchronously ? (await context.Resources.GetBitmapAsync (file).ConfigureAwait (false)) : context.Resources.GetBitmap (file);

			if (bitmap == null)
			{
				Internals.Log.Warning(nameof(FileImageSourceHandler), "Could not find image or image file was invalid: {0}", imagesource);
			}

			return bitmap;
		}

		public async Task<AnimationDrawable> LoadImageAnimationAsync(ImageSource imagesource, Context context, CancellationToken cancelationToken = default(CancellationToken))
		{
			string file = ((FileImageSource)imagesource).File;

			BitmapFactory.Options options = new BitmapFactory.Options();
			options.InJustDecodeBounds = true;

			if (!DecodeSynchronously)
				await BitmapFactory.DecodeResourceAsync(context.Resources, ResourceManager.GetDrawableByName(file), options);
			else
				BitmapFactory.DecodeResource(context.Resources, ResourceManager.GetDrawableByName(file), options);

			var decoder = new GIFDecoder(context, options.InDensity, options.InTargetDensity);
			using (var stream = context.Resources.OpenRawResource(ResourceManager.GetDrawableByName(file)))
			{
				if (!DecodeSynchronously)
					await decoder.ReadGifAsync(stream);
				else
					decoder.ReadGifAsync(stream).Wait();
			}

			return decoder.Animation;
		}
	}
}