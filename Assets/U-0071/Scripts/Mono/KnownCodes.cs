using System;
using System.Collections.Generic;
using U0071;
using UnityEngine;

[Serializable]
public struct KnownCode
{
	public uint Cycle;
	public AreaAuthorization Authorization;
	public int FirstDigit;
	public int SecondDigit;
	public int ThirdDigit;
	public int FourthDigit;

	public bool IsPartiallyDiscovered()
	{
		return
			FirstDigit != -1 ||
			SecondDigit != -1 ||
			ThirdDigit != -1 ||
			FourthDigit != -1;
	}

	public override string ToString()
	{
		return
			GetDigitString(FirstDigit) + 
			GetDigitString(SecondDigit) + 
			GetDigitString(ThirdDigit) + 
			GetDigitString(FourthDigit) +
			" - " + Utilities.GetAuthorizationText(Authorization) +
			" - CYCLE " + Cycle.ToString();
	}

	private string GetDigitString(int digit)
	{
		return digit == -1 ? "?" : digit.ToString();
	}
}

public class KnownCodes : MonoBehaviour
{
	public static KnownCodes Instance;
	public List<KnownCode> Codes = new List<KnownCode>();

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Destroy(gameObject);
		}

		DontDestroyOnLoad(this);
	}

	public void UpdateKnownCode(KnownCode code)
	{
		int index = Codes.Count;
		for (int i = 0; i < Codes.Count; i++)
		{
			KnownCode checkedCode = Codes[i];
			if (code.Cycle < checkedCode.Cycle || 
				code.Cycle == checkedCode.Cycle && code.Authorization < checkedCode.Authorization)
			{
				// insert new code
				index = i;
				break;
			}
			else if (code.Cycle == checkedCode.Cycle && code.Authorization == checkedCode.Authorization)
			{
				// replace code
				index = -1;
				Codes[i] = code;
				break;
			}
		}
		if (index != -1)
		{
			Codes.Insert(index, code);
		}
	}

	public KnownCode GetKnownCode(uint cycle, AreaAuthorization authorization)
	{
		for (int i = 0; i < Codes.Count; i++)
		{
			KnownCode code = Codes[i];
			if (code.Cycle == cycle && code.Authorization == authorization)
			{
				return code;
			}
		}
		return new KnownCode
		{
			Cycle = cycle,
			Authorization = authorization,
			FirstDigit = -1,
			SecondDigit = -1,
			ThirdDigit = -1,
			FourthDigit = -1,
		};
	}
}