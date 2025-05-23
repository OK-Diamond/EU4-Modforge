﻿using System.Diagnostics;
using Editor;
using Editor.DataClasses.MapModes;
using Editor.DataClasses.Saveables;
using Editor.ErrorHandling;
using Editor.Forms.PopUps;
using Editor.Helper;
using Editor.Loading.Enhanced.PCFL.Implementation;
using Editor.Loading.Enhanced.PCFL.Implementation.CountryScope;
using Editor.Loading.Enhanced.PCFL.Implementation.ProvinceScope;
using Editor.Saving;

public static class Executor // Vader is operating it
{
   /*
    * DONE No Commands when running files
    * Optimize ITarget list -> after history release
    * DONE Make command take in TAG / provinceId
    * History Parsing
    *
    */

   public static void ParseScope()
   {
      // check if scope
      // check if province
      // check if country
      // check if area
      // check if region
      // check if continent
      // check if superregion
      // check if trade node
      // check if trade company
      // check if provincegroup
   }


   public static (int parsing, int execution) ExecuteFile(string filePath, ITarget target)
   {
      var sw = Stopwatch.StartNew();

      var po = PathObj.FromPath(filePath);
      var errorCnt = LogManager.TotalErrorCount;
      var effect = target switch
      {
         Country => Effect.ConstructEffect(po, Scopes.Country, target, true),
         Province => Effect.ConstructEffect(po, Scopes.Province, target, true),
         _ => throw new EvilActions("Not yet...")
      };
      if (errorCnt != LogManager.TotalErrorCount)
      {
         var retVal = ImprovedMessageBox.Show("Execution failed due to an error in the file. Please check the error log (F10) for more information!", "Execution failed", ref Globals.Settings.PopUps.TryContinueExecutionPopUp, MessageBoxButtons.RetryCancel, MessageBoxIcon.Exclamation);

         if (retVal != DialogResult.Retry)
            return ((int)sw.ElapsedMilliseconds, -1);

         // continue
      }

      Globals.State = State.Loading; //TODO option for compacting
      var parsing = (int)sw.ElapsedMilliseconds;
      sw.Restart();
      effect.Activate();
      var execution = (int)sw.ElapsedMilliseconds;
      Globals.State = State.Running;
      MapModeManager.RenderCurrent();

      return (parsing, execution);
   }
}