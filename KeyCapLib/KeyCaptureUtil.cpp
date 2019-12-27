////////////////////////////////////////////////////////////////////////////////
// The MIT License (MIT)
//
// Copyright (c) 2019 Tim Stair
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
////////////////////////////////////////////////////////////////////////////////

#include "keycapturestructs.h"
#include "keycaptureutil.h"

bool IsButtonDownRequired(RemapEntry* pRemapEntry, OutputConfig* pKeyDef)
{
	if (pKeyDef->outputFlag.bToggle)
	{
		return !pRemapEntry->bToggled;
	}

	bool keyDownRequired = true;
	keyDownRequired = pKeyDef->outputFlag.bDown;
	return keyDownRequired;
}

bool IsButtonUpRequired(RemapEntry* pRemapEntry, OutputConfig* pKeyDef)
{
	if (pKeyDef->outputFlag.bToggle)
	{
		return pRemapEntry->bToggled;
	}

	bool keyUpRequired = true;
	keyUpRequired = pKeyDef->outputFlag.bUp;
	return keyUpRequired;
}

char* GetBoolString(BYTE nValue)
{
	return nValue == 0 ? "False" : "True";
}

// NOT THREAD SAFE
char* GetInputConfigDescription(InputConfig inputConfig)
{
	static char buffer[256];
	sprintf_s(buffer, 256, "InputConfig [Key: %d 0x%02x][Alt: %s][Ctrl: %s][Shift: %s]",
		inputConfig.virtualKey,
		inputConfig.virtualKey,
		GetBoolString(inputConfig.inputFlag.bAlt),
		GetBoolString(inputConfig.inputFlag.bControl),
		GetBoolString(inputConfig.inputFlag.bShift));
	return buffer;
}

// NOT THREAD SAFE
char* GetOutputConfigDescription(OutputConfig outputConfig)
{
	static char buffer[256];
	sprintf_s(buffer, 256, "OutputConfig [Key: %d 0x%02x][Alt: %s][Ctrl: %s][Shift: %s][Nothing: %s][Mouse: %s][Delay: %s][Toggle: %s][Down: %s][Up: %s]",
		outputConfig.virtualKey,
		outputConfig.virtualKey,
		GetBoolString(outputConfig.outputFlag.bAlt),
		GetBoolString(outputConfig.outputFlag.bControl),
		GetBoolString(outputConfig.outputFlag.bShift),
		GetBoolString(outputConfig.outputFlag.bDoNothing),
		GetBoolString(outputConfig.outputFlag.bMouseOut),
		GetBoolString(outputConfig.outputFlag.bDelay),
		GetBoolString(outputConfig.outputFlag.bToggle),
		GetBoolString(outputConfig.outputFlag.bDown),
		GetBoolString(outputConfig.outputFlag.bUp)
	);

	return buffer;
}

void ValidateStructs()
{
	// TODO: need to have a constant somewhere...
	LogDebugMessage("InputConfig: %d", sizeof(InputConfig));
	LogDebugMessage("OutputConfig: %d", sizeof(OutputConfig));
	LogDebugMessage("InputFlag: %d", sizeof(InputFlag));
	LogDebugMessage("OutputFlag: %d", sizeof(OutputFlag));
	LogDebugMessage("RemapEntry: %d", sizeof(RemapEntry));
	assert(12 == sizeof(InputConfig)); // if this is invalid the configuration tool and kfg files will not be valid
	assert(12 == sizeof(OutputConfig)); // if this is invalid the configuration tool and kfg files will not be valid
	assert(4 == sizeof(InputFlag));
	assert(4 == sizeof(OutputFlag));
	assert(16 == sizeof(RemapEntry));
}


void LogDebugMessage(const char *format, ...)
{
#ifdef _DEBUG
	char outputchar[1024];
	va_list argptr;
	va_start(argptr, format);
	vsnprintf_s(outputchar, sizeof(outputchar), format, argptr);
	strcat_s(outputchar, sizeof(outputchar), "\n");
	OutputDebugStringA(outputchar);
	va_end(argptr);
#endif
}