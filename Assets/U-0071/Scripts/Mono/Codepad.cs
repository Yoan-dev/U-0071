using System;
using UnityEngine.UIElements;

namespace U0071
{
	public class Codepad : VisualElement
	{
		private VisualElement _root;
		private Label _screenText;
		private CodepadButton[] _buttons;

		private Action _callback;
		private AreaAuthorization _authorization;
		private int _cycleCounter;
		private int _requiredCode;
		private string _code;
		private bool _accessDenied;

		public Codepad(VisualElement root)
		{
			_root = root;
			_screenText = root.Q<Label>("screen_text");
			_buttons = new CodepadButton[12];
			for (int i = 0; i < 10; i++)
			{
				_buttons[i] = new CodepadButton(root.Q<VisualElement>("button_" + i), i, OnNumberButtonClicked);
			}
			_buttons[10] = new CodepadButton(root.Q<VisualElement>("button_v"), 10, OnValidateButtonClicked);
			_buttons[11] = new CodepadButton(root.Q<VisualElement>("button_x"), 11, OnCancelButtonClicked);
		}

		public void ShowCodepad(AreaAuthorization authorization, in CycleComponent cycle, Action callback)
		{
			_code = "";
			_authorization = authorization;
			_cycleCounter = (int)cycle.CycleCounter;
			_requiredCode = cycle.GetCode(authorization);

			if (callback != null)
			{
				// if null, comes from update
				_callback = callback;
			}

			UpdateScreenText();
			_root.style.display = DisplayStyle.Flex;
		}

		public void UpdateCodepad(float deltaTime, in CycleComponent cycle, int numeric, bool validate, bool cancel)
		{
			// live update
			if (cycle.CycleCounter != _cycleCounter)
			{
				// cycle changed, update infos
				ShowCodepad(_authorization, in cycle, null);
			}

			if (numeric != -1)
			{
				OnNumberButtonClicked(numeric);
			}
			else if (validate)
			{
				OnValidateButtonClicked(10);
			}
			else if (cancel)
			{
				OnCancelButtonClicked(11);
			}

			foreach (CodepadButton button in _buttons)
			{
				button.Update(deltaTime);
			}
		}

		public void ExitScreen()
		{
			foreach (CodepadButton button in _buttons)
			{
				button.Reset();
			}
			_accessDenied = false;
			_callback = null;
			_root.style.display = DisplayStyle.None;
		}

		public bool IsShown()
		{
			return _root.style.display == DisplayStyle.Flex;
		}

		private void OnNumberButtonClicked(int number)
		{
			_buttons[number].StartFeedback();
			if (_code.Length < 4)
			{
				_code += number.ToString();
				UpdateScreenText();
			}
			else
			{
				// TODO: negative sound feedback "full"
			}
		}

		private void OnCancelButtonClicked(int i)
		{
			// TODO: sound feedback "reset"
			_buttons[i].StartFeedback();
			_code = "";
			UpdateScreenText();
		}

		private void OnValidateButtonClicked(int i)
		{
			_buttons[i].StartFeedback();
			if (_code.Length < 4)
			{
				// TODO: negative sound feedback "incomplete"
			}
			else if (_code == _requiredCode.ToString("0000"))
			{
				// TODO: positive sound feedback "granted"
				_callback?.Invoke();
				ExitScreen();

				// update known codes
				// (in case of guessed code)
				KnownCodes.Instance.UpdateKnownCode(new KnownCode
				{
					Cycle = (uint)_cycleCounter,
					Authorization = _authorization,
					FirstDigit = int.Parse(_code[0].ToString()),
					SecondDigit = int.Parse(_code[1].ToString()),
					ThirdDigit = int.Parse(_code[2].ToString()),
					FourthDigit = int.Parse(_code[3].ToString()),
				});
			}
			else
			{
				// TODO: negative sound feedback "denied"
				_accessDenied = true;
				_code = "";
				UpdateScreenText();
			}
		}

		private void UpdateScreenText()
		{
			_screenText.text = GetDefaultText() + GetCodeText();
		}

		private string GetDefaultText()
		{
			return (_accessDenied ? "Access Denied\n" : "Required Access\n") + Utilities.GetAuthorizationText(_authorization) + "-Cycle" + _cycleCounter + "\n";
		}

		private string GetCodeText()
		{
			int length = _code.Length;
			return _code + (
				length == 0 ? "____" :
				length == 1 ? "___" :
				length == 2 ? "__" :
				length == 3 ? "_" : "");
		}
	}
}