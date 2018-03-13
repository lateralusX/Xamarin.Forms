using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Foundation;
using UIKit;
using ImageIO;
using CoreAnimation;
using Xamarin.Forms.Internals;
using RectangleF = CoreGraphics.CGRect;
using CoreGraphics;

namespace Xamarin.Forms.Platform.iOS
{
	public static class ImageExtensions
	{
		public static UIViewContentMode ToUIViewContentMode(this Aspect aspect)
		{
			switch (aspect)
			{
				case Aspect.AspectFill:
					return UIViewContentMode.ScaleAspectFill;
				case Aspect.Fill:
					return UIViewContentMode.ScaleToFill;
				case Aspect.AspectFit:
				default:
					return UIViewContentMode.ScaleAspectFit;
			}
		}
	}

	public class ImageRenderer : ViewRenderer<Image, UIImageView>
	{
		bool _isDisposed;
		ImageAnimation _imageAnimation = null;

		protected override void Dispose(bool disposing)
		{
			if (_isDisposed)
				return;

			if (disposing)
			{
				UIImage oldUIImage;
				if (Control != null && (oldUIImage = Control.Image) != null)
				{
					oldUIImage.Dispose();
				}

				if (Control != null && _imageAnimation != null)
				{
					_imageAnimation.Clear(Control);
					_imageAnimation.Dispose();
					_imageAnimation = null;
				}
			}

			_isDisposed = true;

			base.Dispose(disposing);
		}

		protected override async void OnElementChanged(ElementChangedEventArgs<Image> e)
		{
			if (Control == null)
			{
				var imageView = new UIImageView(RectangleF.Empty);
				imageView.ContentMode = UIViewContentMode.ScaleAspectFit;
				imageView.ClipsToBounds = true;
				SetNativeControl(imageView);
			}

			if (e.NewElement != null)
			{
				SetAspect();
				await TrySetImage(e.OldElement);
				SetOpacity();
			}

			base.OnElementChanged(e);
		}

		protected override async void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			base.OnElementPropertyChanged (sender, e);
			if (e.PropertyName == Image.SourceProperty.PropertyName)
				await TrySetImage ();
			else if (e.PropertyName == Image.IsOpaqueProperty.PropertyName)
				SetOpacity ();
			else if (e.PropertyName == Image.AspectProperty.PropertyName)
				SetAspect ();
			else if (e.PropertyName == Image.IsAnimationPlayingProperty.PropertyName)
				StartStopAnimation ();
		}

		public override CGSize SizeThatFits(CGSize size)
		{
			if (Control != null && Control.Image == null && _imageAnimation != null)
			{
				return new CoreGraphics.CGSize (_imageAnimation.Width, _imageAnimation.Height);
			}

			return base.SizeThatFits (size);
		}

		void ClearImageData()
		{
			if (Control != null)
			{
				Control.Image = null;
				ClearAnimationData ();
			}
		}

		void ClearAnimationData(ref ImageAnimation animation)
		{
			if (animation != null)
			{
				if (Control != null)
				{
					animation.Clear(Control);
				}
				animation.Dispose ();
				animation = null;
			}
		}

		void ClearAnimationData()
		{
			ClearAnimationData (ref _imageAnimation);
		}

		void SetAspect()
		{
			if (_isDisposed || Element == null || Control == null)
			{
				return;
			}

			Control.ContentMode = Element.Aspect.ToUIViewContentMode();
		}

		protected virtual async Task TrySetImage(Image previous = null)
		{
			// By default we'll just catch and log any exceptions thrown by SetImage so they don't bring down
			// the application; a custom renderer can override this method and handle exceptions from
			// SetImage differently if it wants to

			try
			{
				await SetImage(previous).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Warning(nameof(ImageRenderer), "Error loading image: {0}", ex);
			}
			finally
			{
				((IImageController)Element)?.SetIsLoading(false);
			}
		}

		protected async Task SetImage(Image oldElement = null)
		{
			if (_isDisposed || Element == null || Control == null)
			{
				return;
			}

			var source = Element.Source;

			if (oldElement != null)
			{
				var oldSource = oldElement.Source;
				if (Equals(oldSource, source))
					return;

				if (oldSource is FileImageSource && source is FileImageSource && ((FileImageSource)oldSource).File == ((FileImageSource)source).File)
					return;

				ClearAnimationData ();
			}

			IImageSourceHandlerEx handler;

			Element.SetIsLoading(true);

			if (source != null &&
			    (handler = Internals.Registrar.Registered.GetHandlerForObject<IImageSourceHandlerEx>(source)) != null)
			{
				UIImage uiimage = null;
				ImageAnimation animation = null;
				try
				{
					if (!Element.IsSet (Image.AnimationPlayBehaviorProperty) && !Element.IsSet (Image.IsAnimationPlayingProperty))
						uiimage = await handler.LoadImageAsync (source, scale: (float)UIScreen.MainScreen.Scale);
					else
						animation = await handler.LoadImageAnimationAsync (source, scale: (float)UIScreen.MainScreen.Scale);
				}
				catch (OperationCanceledException)
				{
					uiimage = null;
					ClearAnimationData (ref animation);
				}

				if (_isDisposed)
				{
					ClearAnimationData (ref animation);
					return;
				}

				var imageView = Control;
				if (imageView != null)
				{
					if (uiimage != null)
						imageView.Image = uiimage;
					else if (animation != null)
					{
						_imageAnimation = animation;
						if ((Image.ImagePlayBehavior)Element.GetValue (Image.AnimationPlayBehaviorProperty) == Image.ImagePlayBehavior.OnLoad)
							_imageAnimation.Start(Control);
						else
							_imageAnimation.Start(Control, true);
					}
				}

				((IVisualElementController)Element).NativeSizeChanged();
			}
			else
			{
				ClearImageData ();
			}

			Element.SetIsLoading(false);
		}

		void SetOpacity()
		{
			if (_isDisposed || Element == null || Control == null)
			{
				return;
			}

			Control.Opaque = Element.IsOpaque;
		}

		void StartStopAnimation()
		{
			if (_isDisposed || Element == null || Control == null || _imageAnimation == null)
			{
				return;
			}

			if (Element.IsLoading)
				return;

			if (Element.IsAnimationPlaying)
				_imageAnimation.Start(Control);
			else
				_imageAnimation.Stop(Control);
		}
	}

	public class ImageAnimation : IDisposable
	{
		CAKeyFrameAnimation _animation;
		bool _disposed = false;

		public ImageAnimation(CAKeyFrameAnimation animation, int width, int height)
		{
			_animation = animation;
			Width = width;
			Height = height;
		}

		public int Width { get; set; }

		public int Height { get; set; }

		public void Start(UIImageView imageView, bool paused = false)
		{
			Clear(imageView);
			if (_animation != null)
			{
				imageView.Layer.AddAnimation (_animation, "ImageRenderLayerAnimation");
				if (paused)
					Pause(imageView);
				else
					Resume(imageView);
			}
		}

		public void Pause(UIImageView imageView)
		{
			imageView.Layer.Speed = 0.0f;
		}

		public void Resume(UIImageView imageView)
		{
			imageView.Layer.Speed = 1.0f;
		}

		public void Stop(UIImageView imageView)
		{
			// Reset to the begining of the animation in paused mode.
			Start (imageView, true);
		}

		public void Clear(UIImageView imageView)
		{
			imageView.Layer.RemoveAnimation ("ImageRenderLayerAnimation");
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			if (_animation != null)
			{
				_animation.Dispose ();
				_animation = null;
			}

			_disposed = true;
		}

		public void Dispose()
		{
			Dispose (true);
		}
	}

	public class ImageAnimationHelper
	{
		protected class ImageDataHelper : IDisposable
		{
			NSObject[] _keyFrames = null;
			NSNumber[] _keyTimes = null;
			double[] _delayTimes = null;
			int _imageCount = 0;
			double _totalAnimationTime = 0.0f;
			bool _disposed = false;

			public ImageDataHelper(nint imageCount)
			{
				if (imageCount <= 0)
					throw new ArgumentException();

				_keyFrames = new NSObject[imageCount];
				_keyTimes = new NSNumber[imageCount];
				_delayTimes = new double[imageCount];
				_imageCount = (int)imageCount;
				Width = 0;
				Height = 0;
			}

			public int Width { get; set; }
			public int Height { get; set; }

			public void AddFrameData(int index, CGImageSource imageSource)
			{
				if (index < 0 || index >= _imageCount || index >= imageSource.ImageCount)
					throw new ArgumentException();

				double delayTime = 0.1f;

				var imageProperties = imageSource.GetProperties (index, null);
				using (var gifImageProperties = imageProperties?.Dictionary[ImageIO.CGImageProperties.GIFDictionary])
				using (var unclampedDelayTimeValue = gifImageProperties?.ValueForKey (ImageIO.CGImageProperties.GIFUnclampedDelayTime))
				using (var delayTimeValue = gifImageProperties?.ValueForKey (ImageIO.CGImageProperties.GIFDelayTime))
				{
					if (unclampedDelayTimeValue != null)
						double.TryParse (unclampedDelayTimeValue.ToString (), out delayTime);
					else if (delayTimeValue != null)
						double.TryParse (delayTimeValue.ToString (), out delayTime);

					if (delayTime < 0.01f)
						delayTime = 0.01f;

					using (var image = imageSource.CreateImage(index, null))
					{
						if (image != null)
						{
							Width = Math.Max (Width, (int)image.Width);
							Height = Math.Max (Height, (int)image.Height);

							if (_keyFrames[index] != null)
							{
								_keyFrames[index].Dispose ();
								_keyFrames[index] = null;
							}

							_keyFrames[index] = NSObject.FromObject (image);
							_delayTimes[index] = delayTime;
							_totalAnimationTime += delayTime;
						}
					}
				}
			}

			public CAKeyFrameAnimation CreateKeyFrameAnimation()
			{
				if (_totalAnimationTime <= 0.0f)
					return null;

				double currentTime = 0.0f;
				for (int i = 0; i < _imageCount; i++)
				{
					currentTime += _delayTimes[i] / _totalAnimationTime;

					if (_keyTimes[i] != null)
					{
						_keyTimes[i].Dispose ();
						_keyTimes[i] = null;
					}

					_keyTimes[i] = new NSNumber(currentTime);
				}

				return new CAKeyFrameAnimation {
					Values = _keyFrames,
					KeyTimes = _keyTimes,
					Duration = _totalAnimationTime
				};
			}

			protected virtual void Dispose(bool disposing)
			{
				if (_disposed)
					return;

				for (int i = 0; i < _imageCount; i++)
				{
					if (_keyFrames[i] != null)
					{
						_keyFrames[i].Dispose ();
						_keyFrames[i] = null;
					}

					if (_keyTimes[i] != null)
					{
						_keyTimes[i].Dispose ();
						_keyTimes[i] = null;
					}
				}

				_disposed = true;
			}

			public void Dispose()
			{
				Dispose(true);
			}
		}

		static public ImageAnimation CreateAnimationFromImageSource(CGImageSource imageSource)
		{
			ImageAnimation animation = null;
			float repeatCount = float.MaxValue;
			var imageCount = imageSource.ImageCount;

			if (imageCount <= 0)
				return null;

			using (var imageData = new ImageDataHelper (imageCount))
			{
				if (imageSource.TypeIdentifier == "com.compuserve.gif")
				{
					var imageProperties = imageSource.GetProperties (null);
					using (var gifImageProperties = imageProperties?.Dictionary[ImageIO.CGImageProperties.GIFDictionary])
					using (var repeatCountValue = gifImageProperties?.ValueForKey (ImageIO.CGImageProperties.GIFLoopCount))
					{
						if (repeatCountValue != null)
							float.TryParse (repeatCountValue.ToString (), out repeatCount);
					}
				}

				for (int i = 0; i < imageCount; i++)
				{
					imageData.AddFrameData (i, imageSource);
				}

				var keyFrameAnimation = imageData.CreateKeyFrameAnimation ();
				if (keyFrameAnimation != null)
				{
					keyFrameAnimation.CalculationMode = CAAnimation.AnimationDiscrete;
					keyFrameAnimation.RemovedOnCompletion = false;
					keyFrameAnimation.KeyPath = "contents";
					keyFrameAnimation.RepeatCount = repeatCount;

					if (imageCount == 1)
					{
						keyFrameAnimation.Duration = double.MaxValue;
						keyFrameAnimation.KeyTimes = null;
					}

					animation = new ImageAnimation (keyFrameAnimation, imageData.Width, imageData.Height);
				}
			}

			return animation;
		}
	}

	public interface IImageSourceHandler : IRegisterable {
		Task<UIImage> LoadImageAsync(ImageSource imagesource, CancellationToken cancelationToken = default (CancellationToken), float scale = 1);
	}

	public interface IImageSourceHandlerEx : IImageSourceHandler {
		Task<ImageAnimation> LoadImageAnimationAsync(ImageSource imagesource, CancellationToken cancelationToken = default (CancellationToken), float scale = 1);
	}

	public sealed class FileImageSourceHandler : IImageSourceHandlerEx
	{
		public Task<UIImage> LoadImageAsync(ImageSource imagesource, CancellationToken cancelationToken = default(CancellationToken), float scale = 1f)
		{
			UIImage image = null;
			var filesource = imagesource as FileImageSource;
			var file = filesource?.File;
			if (!string.IsNullOrEmpty(file))
				image = File.Exists(file) ? new UIImage(file) : UIImage.FromBundle(file);

			if (image == null)
			{
				Log.Warning(nameof(FileImageSourceHandler), "Could not find image: {0}", imagesource);
			}

			return Task.FromResult(image);
		}

		public Task<ImageAnimation> LoadImageAnimationAsync(ImageSource imagesource, CancellationToken cancelationToken = default (CancellationToken), float scale = 1)
		{
			ImageAnimation animation = null;
			var fileSoure = imagesource as FileImageSource;

			var file = fileSoure?.File;
			if (!string.IsNullOrEmpty (file) && File.Exists(file))
			{
				using (var parsedImageSource = CGImageSource.FromUrl (NSUrl.CreateFileUrl (file, null)))
				{
					animation = ImageAnimationHelper.CreateAnimationFromImageSource (parsedImageSource);
				}
			}

			if (animation == null)
			{
				Log.Warning (nameof (FileImageSourceHandler), "Could not find image: {0}", imagesource);
			}

			return Task.FromResult (animation);
		}
	}

	public sealed class StreamImagesourceHandler : IImageSourceHandlerEx
	{
		public async Task<UIImage> LoadImageAsync(ImageSource imagesource, CancellationToken cancelationToken = default(CancellationToken), float scale = 1f)
		{
			UIImage image = null;
			var streamsource = imagesource as StreamImageSource;
			if (streamsource?.Stream != null)
			{
				using (var streamImage = await ((IStreamImageSource)streamsource).GetStreamAsync(cancelationToken).ConfigureAwait(false))
				{
					if (streamImage != null)
						image = UIImage.LoadFromData(NSData.FromStream(streamImage), scale);
				}
			}

			if (image == null)
			{
				Log.Warning(nameof(StreamImagesourceHandler), "Could not load image: {0}", streamsource);
			}

			return image;
		}

		public Task<ImageAnimation> LoadImageAnimationAsync(ImageSource imagesource, CancellationToken cancelationToken = default (CancellationToken), float scale = 1)
		{
			return null;
		}
	}

	public sealed class ImageLoaderSourceHandler : IImageSourceHandlerEx
	{
		public async Task<UIImage> LoadImageAsync(ImageSource imagesource, CancellationToken cancelationToken = default(CancellationToken), float scale = 1f)
		{
			UIImage image = null;
			var imageLoader = imagesource as UriImageSource;
			if (imageLoader?.Uri != null)
			{
				using (var streamImage = await imageLoader.GetStreamAsync(cancelationToken).ConfigureAwait(false))
				{
					if (streamImage != null)
						image = UIImage.LoadFromData(NSData.FromStream(streamImage), scale);
				}
			}

			if (image == null)
			{
				Log.Warning(nameof(ImageLoaderSourceHandler), "Could not load image: {0}", imageLoader);
			}

			return image;
		}

		public Task<ImageAnimation> LoadImageAnimationAsync(ImageSource imagesource, CancellationToken cancelationToken = default (CancellationToken), float scale = 1)
		{
			return null;
		}
	}
}