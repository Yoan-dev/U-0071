using System;
using UnityEngine.UIElements;

namespace U0071
{
	public class Codepad : VisualElement
	{
		private VisualElement _root;
		private Label _screenText;
		private Button[] _numberButtons;
		private Button _cancelButton;
		private Button _validateButton;
		private Action _callback;

		private AreaAuthorization _authorization;
		private int _cycleCounter;
		private int _requiredCode;
		private string _code;

		public Codepad(VisualElement root)
		{
			_root = root;
			_screenText = root.Q<Label>("screen_text");
			_cancelButton = root.Q<Button>("button_X");
			_validateButton = root.Q<Button>("button_V");
			_numberButtons = new Button[10];
			for (int i = 0; i < _numberButtons.Length; i++)
			{
				Button button = root.Q<Button>("button_" + i);
				button.RegisterCallback<ClickEvent>(clickEvent => OnNumberButtonClicked(i));
				_numberButtons[i] = button;
			}
			_cancelButton.RegisterCallback<ClickEvent>(clickEvent => OnCancelButtonClicked());
			_validateButton.RegisterCallback<ClickEvent>(clickEvent => OnValidateButtonClicked());
		}

		public void ShowCodepad(AreaAuthorization authorization, in CycleComponent cycle, Action callback)
		{
			_code = "";
			_authorization = authorization;
			_cycleCounter = (int)cycle.CycleCounter;
			_requiredCode =
				authorization == AreaAuthorization.LevelOne ? cycle.LevelOneCode :
				authorization == AreaAuthorization.LevelTwo ? cycle.LevelTwoCode :
				authorization == AreaAuthorization.LevelThree ? cycle.LevelThreeCode :
				authorization == AreaAuthorization.Red ? cycle.RedCode :
				authorization == AreaAuthorization.Blue ? cycle.BlueCode :
				authorization == AreaAuthorization.Yellow ? cycle.YellowCode : 1234;
			
			UpdateScreenText();
			_root.style.display = DisplayStyle.Flex;
		}

		public void UpdateCodepad(in CycleComponent cycle)
		{
			// live update
			// TODO: can click on buttons but does nothing
			// TODO: reset text (effect tbd how ? coroutine ? tick from UIManager ?)
			// TBD
		}

		public void ExitScreen()
		{
			_callback = null;
			_root.style.display = DisplayStyle.None;
		}

		private void OnNumberButtonClicked(int number)
		{
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

		private void OnCancelButtonClicked()
		{
			// TODO: sound feedback "reset"
			_code = "";
			UpdateScreenText();
		}

		private void OnValidateButtonClicked()
		{
			if (_code.Length < 4)
			{
				// TODO: negative sound feedback "incomplete"
			}
			else if (_code == _requiredCode.ToString("0000"))
			{
				// TODO: positive sound feedback "granted"
				_callback?.Invoke();
				// TODO: lock screen until door is closed (TBD)
			}
			else
			{
				// TODO: negative sound feedback "denied"
				UpdateScreenText();
			}
		}

		private void UpdateScreenText()
		{
			// TODO: depends on context (ACCESS DENIED / GRANTED / RESET / BEHAVE)
			_screenText.text = GetDefaultText() + GetCodeText();
		}

		private string GetDefaultText()
		{
			return "Required Access\n" + GetAuthorizationText() + "-Cycle" + _cycleCounter + "\n";
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

		private string GetAuthorizationText()
		{
			return _authorization switch
			{
				AreaAuthorization.LevelOne => "LVL1",
				AreaAuthorization.LevelTwo => "LVL2",
				AreaAuthorization.LevelThree => "LVL3",
				AreaAuthorization.Red => "RED",
				AreaAuthorization.Blue => "BLUE",
				AreaAuthorization.Yellow => "YELLOW",
				_ => "ERROR",
			};
		}
	}
}