using System;
using System.Collections.Generic;
using System.Text;

using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.CustomAttributes;
using Xamarin.Forms.PlatformConfiguration;

#if UITEST
using Xamarin.UITest;
using NUnit.Framework;
#endif

namespace Xamarin.Forms.Controls.Issues
{
	[Preserve(AllMembers = true)]
	[Issue(IssueTracker.Github, 1704, "[Enhancement] Basic gif animation features", PlatformAffected.UWP)]
	public class Issue1704 : TestContentPage
	{
		Image _regularImage;
		Image _animateImageOnLoad;
		Image _animateImageOnStart;
		//Image _animatedImageFromUriOnStart;
		Button _startStop;
		bool _start = true;

		protected override void Init()
		{
			var regularImageLabel = new Label { Text = "No animation." };
			regularImageLabel.FontSize = 12;
			_regularImage = new Image();
			_regularImage.Source = "ie_retro.gif";

			var animateImageOnLoadLabel = new Label { Text = "Animation played on load." };
			animateImageOnLoadLabel.FontSize = 12;
			_animateImageOnLoad = new Image();
			_animateImageOnLoad.AnimationPlayBehavior = Image.AnimationPlayBehaviorValue.OnLoad;
			_animateImageOnLoad.Source = "ie_retro.gif";

			var animateImageOnStartLabel = new Label { Text = "Animation played on start." };
			animateImageOnStartLabel.FontSize = 12;
			_animateImageOnStart = new Image();
			_animateImageOnStart.AnimationPlayBehavior = Image.AnimationPlayBehaviorValue.OnStart;
			_animateImageOnStart.Source = "sweden.gif";

			//var animateImageFromUriOnStartLabel = new Label { Text = "Animation from URI played on start." };
			//animateImageFromUriOnStartLabel.FontSize = 12;
			//_animatedImageFromUriOnStart = new Image();
			//_animatedImageFromUriOnStart.AnimationPlayBehavior = Image.AnimationPlayBehaviorValue.OnStart;
			//_animatedImageFromUriOnStart.Source = ImageSource.FromUri(new Uri("http://media.giphy.com/media/mf8UbIDew7e8g/giphy.gif"));
			//_animatedImageFromUriOnStart.Scale = 0.1;

			_startStop = new Button { Text = "Start Animation" };
			_startStop.Clicked += (object sender, EventArgs e) => {
				if (_start)
				{
					if (!_regularImage.IsAnimationPlaying)
						_regularImage.StartAnimation(); // Shouldn't have any effect.
					if (!_animateImageOnLoad.IsAnimationPlaying)
						_animateImageOnLoad.StartAnimation();
					if (!_animateImageOnStart.IsAnimationPlaying)
						_animateImageOnStart.StartAnimation();
					//if (!_animatedImageFromUriOnStart.IsAnimationPlaying)
					//	_animatedImageFromUriOnStart.StartAnimation();

					_startStop.Text = "Stop Animation";
					_start = false;
				}
				else
				{
					if (_regularImage.IsAnimationPlaying)
						_regularImage.StopAnimation(); // Shouldn't have any effect.
					if (_animateImageOnLoad.IsAnimationPlaying)
						_animateImageOnLoad.StopAnimation();
					if (_animateImageOnStart.IsAnimationPlaying)
						_animateImageOnStart.StopAnimation();
					//if (_animatedImageFromUriOnStart.IsAnimationPlaying)
					//	_animatedImageFromUriOnStart.StopAnimation();

					_startStop.Text = "Start Animation";
					_start = true;
				}
			};

			Content = new StackLayout {
				Children = {
					regularImageLabel,
					_regularImage,
					animateImageOnLoadLabel,
					_animateImageOnLoad,
					animateImageOnStartLabel,
					_animateImageOnStart,
					//animateImageFromUriOnStartLabel,
					//_animatedImageFromUriOnStart,
					_startStop
				}
			};
		}
	}
}
