using System;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Graphics.Drawables;
using AImageView = Android.Widget.ImageView;

namespace Xamarin.Forms.Platform.Android
{

	internal static class ImageViewExtensions
	{
		public static async Task<AnimationDrawable> UpdateAnimation(this AImageView imageView, Image newImage, Image previousImage = null)
		{
			var result = await InternalUpdateBitmap(imageView, newImage, previousImage, true);
			result.Item1?.Dispose();

			return result.Item2;
		}

		public static async Task UpdateBitmap(this AImageView imageView, Image newImage, Image previousImage = null)
		{
			var result = await InternalUpdateBitmap(imageView, newImage, previousImage, false);
			result.Item1?.Dispose();
			result.Item2?.Dispose();
		}

		// TODO hartez 2017/04/07 09:33:03 Review this again, not sure it's handling the transition from previousImage to 'null' newImage correctly
		internal static async Task<Tuple<Bitmap,AnimationDrawable>> InternalUpdateBitmap(AImageView imageView, Image newImage, Image previousImage, bool useAnimation)
		{
			if (imageView == null || imageView.IsDisposed())
				return new Tuple<Bitmap, AnimationDrawable>(null,null);

			if (Device.IsInvokeRequired)
				throw new InvalidOperationException("Image Bitmap must not be updated from background thread");

			if (previousImage != null && Equals(previousImage.Source, newImage.Source))
				return new Tuple<Bitmap, AnimationDrawable>(null,null);

			var imageController = newImage as IImageController;

			imageController?.SetIsLoading(true);

			(imageView as IImageRendererController)?.SkipInvalidate();

			imageView.SetImageResource(global::Android.Resource.Color.Transparent);

			ImageSource source = newImage?.Source;
			Bitmap bitmap = null;
			AnimationDrawable animation = null;
			IImageSourceHandlerEx handler;

			if (source != null && (handler = Internals.Registrar.Registered.GetHandlerForObject<IImageSourceHandlerEx>(source)) != null)
			{
				try
				{
					if (!useAnimation)
						bitmap = await handler.LoadImageAsync(source, imageView.Context);
					else
						animation = await handler.LoadImageAnimationAsync(source, imageView.Context);
				}
				catch (TaskCanceledException)
				{
					imageController?.SetIsLoading(false);
				}
			}

			if (newImage == null || !Equals(newImage.Source, source))
			{
				bitmap?.Dispose();
				animation?.Dispose();
				new Tuple<Bitmap, AnimationDrawable>(null, null);
			}

			if (!imageView.IsDisposed())
			{
				if (bitmap == null && animation == null && source is FileImageSource)
					imageView.SetImageResource(ResourceManager.GetDrawableByName(((FileImageSource)source).File));
				else
				{
					if (bitmap != null)
						imageView.SetImageBitmap(bitmap);
					else if (animation != null)
						imageView.SetImageDrawable(animation);
				}
			}

			imageController?.SetIsLoading(false);
			((IVisualElementController)newImage).NativeSizeChanged();

			return new Tuple<Bitmap, AnimationDrawable>(bitmap, animation);
		}
	}
}
