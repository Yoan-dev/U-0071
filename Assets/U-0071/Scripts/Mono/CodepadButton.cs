using System;
using UnityEngine.UIElements;

namespace U0071
{
	public class CodepadButton : VisualElement
	{
		private VisualElement _root;
		private int _number;
		private Action<int> _callback;
		private float feedbackTimer;

		public CodepadButton(VisualElement root, int number, Action<int> callback)
		{
			_root = root;
			_number = number;
			_callback = callback;
			_root.RegisterCallback<ClickEvent>(ClickEvent => OnClicked());
		}

		public void StartFeedback()
		{
			if (feedbackTimer <= 0f)
			{
				_root.AddToClassList("jam-button-codepad-pressed");
			}
			feedbackTimer = Const.CodepadButtonFeedbackTime;
		}

		public void Update(float deltaTime)
		{
			if (feedbackTimer > 0f)
			{
				feedbackTimer -= deltaTime;
				if (feedbackTimer <= 0f)
				{
					_root.RemoveFromClassList("jam-button-codepad-pressed");
				}
			}
		}

		private void OnClicked()
		{
			_callback?.Invoke(_number);
		}
	}
}