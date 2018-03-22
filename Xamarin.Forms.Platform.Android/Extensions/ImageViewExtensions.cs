using System;
using System.Threading.Tasks;
using Android.Graphics;
using Android.Graphics.Drawables;
using AImageView = Android.Widget.ImageView;

namespace Xamarin.Forms.Platform.Android
{

	internal static class ImageViewExtensions
	{
		public static void Reset(this AnimationDrawable animation)
		{
			if (!animation.IsDisposed())
			{
				animation.Stop();
				int frameCount = animation.NumberOfFrames;
				for (int i = 0; i < frameCount; i++)
				{
					var currentFrame = animation.GetFrame(i);
					if (currentFrame is BitmapDrawable bitmapDrawable)
					{
						var bitmap = bitmapDrawable.Bitmap;
						if (bitmap != null)
						{
							if (!bitmap.IsRecycled)
							{
								bitmap.Recycle();
							}
							bitmap.Dispose();
							bitmap = null;
						}
						bitmapDrawable.Dispose();
						bitmapDrawable = null;
					}
					currentFrame = null;
				}
				animation = null;
			}
		}

		public static void Reset(this AImageView imageView)
		{
			if (!imageView.IsDisposed())
			{
				if (imageView.Drawable is AnimationDrawable animation)
				{
					imageView.SetImageDrawable(null);
					animation.Reset();
					animation.Dispose();
					animation = null;
				}

				imageView.SetImageResource(global::Android.Resource.Color.Transparent);
			}
		}

		// TODO hartez 2017/04/07 09:33:03 Review this again, not sure it's handling the transition from previousImage to 'null' newImage correctly
		public static async Task UpdateBitmap(this AImageView imageView, Image newImage, Image previousImage = null)
		{
			if (imageView == null || imageView.IsDisposed())
				return;

			if (Device.IsInvokeRequired)
				throw new InvalidOperationException("Image Bitmap must not be updated from background thread");

			if (previousImage != null && Equals(previousImage.Source, newImage.Source))
				return;

			var imageController = newImage as IImageController;

			imageController?.SetIsLoading(true);

			(imageView as IImageRendererController)?.SkipInvalidate();

			imageView.Reset();

			ImageSource source = newImage?.Source;
			Bitmap bitmap = null;
			AnimationDrawable animation = null;
			IImageSourceHandlerEx handler;
			bool useAnimation = newImage.IsSet(Image.AnimationPlayBehaviorProperty) || newImage.IsSet(Image.IsAnimationPlayingProperty);

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

			if (newImage == null || !Equals(newImage.Source, source) || imageView.IsDisposed())
			{
				bitmap?.Dispose();
				animation?.Reset();
				animation?.Dispose();
				return;
			}

			if (bitmap == null && animation == null && source is FileImageSource)
			{
				imageView.SetImageResource(ResourceManager.GetDrawableByName(((FileImageSource)source).File));
			}
			else if (bitmap != null && animation == null)
			{
				imageView.SetImageBitmap(bitmap);
			}
			else if (animation != null && bitmap == null)
			{
				imageView.SetImageDrawable(animation);
				if ((Image.AnimationPlayBehaviorValue)newImage.GetValue(Image.AnimationPlayBehaviorProperty) == Image.AnimationPlayBehaviorValue.OnLoad)
					animation.Start();
			}
			
			imageController?.SetIsLoading(false);
			((IVisualElementController)newImage).NativeSizeChanged();
		}
	}
}
