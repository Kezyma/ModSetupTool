import mobase
import os
from pathlib import Path
from subprocess import Popen, PIPE

class ModSetupTool(mobase.IPlugin):

    def __init__(self):
        super().__init__()
        
    def init(self, organiser):
        self._organiser = organiser

        setupComplete = self._organiser.pluginSetting("ModSetupTool", "setupcomplete")
        if setupComplete == False:
            moPath = Path(__file__).parent.parent
            setupCompletePath = moPath / ".setup_complete.txt"
            setupProgressPath = moPath / ".setup_in_progress.txt"
            if setupProgressPath.exists() == False:
                if setupCompletePath.exists():
                    self._organiser.setPluginSetting("ModSetupTool", "setupcomplete", True)
                    os.remove(str(setupCompletePath))
                else:
                    mowjPath = str(moPath / "ModSetup.exe")
                    CREATE_NEW_PROCESS_GROUP = 0x00000200
                    DETACHED_PROCESS = 0x00000008
                    p = Popen([mowjPath], stdin=PIPE, stdout=PIPE, stderr=PIPE, creationflags=DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP)
                    os.system("taskkill /im ModOrganizer.exe /f")
        
    def author(self):
        return "Kezyma"
    
    def description(self):
        return "Forces ModSetup.exe to be run before Mod Organizer 2 launches."
    
    def enabledByDefault(self):
        return True
    
    def localizedName(self):
        return "Mod Setup Tool"
    
    def name(self):
        return "ModSetupTool"

    def version(self):
        return mobase.VersionInfo(1, 0, 0)

    def settings(self):
        return [
            mobase.PluginSetting("setupcomplete","Records whether setup has been completed.", False)
            ]

def createPlugin():
    return ModSetupTool()