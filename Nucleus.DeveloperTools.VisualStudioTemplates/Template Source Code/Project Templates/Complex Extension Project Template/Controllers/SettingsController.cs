﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Nucleus.Abstractions;
using Nucleus.Abstractions.Models;
using Nucleus.Abstractions.Models.FileSystem;
using Nucleus.Abstractions.Managers;
using Nucleus.Extensions;
using $nucleus.extension.namespace$.Models;

namespace $nucleus.extension.namespace$.Controllers
{
	[Extension("$nucleus.extension.name$")]
	[Authorize(Policy = Nucleus.Abstractions.Authorization.Constants.MODULE_EDIT_POLICY)]
	public class $nucleus.extension.name$SettingsController : Controller
	{
		private Context Context { get; }
		private IPageModuleManager PageModuleManager { get; }
		private $nucleus.extension.name$Manager $nucleus.extension.name$Manager { get; }

		public $nucleus.extension.name$SettingsController(Context Context, IPageModuleManager pageModuleManager, $nucleus.extension.name$Manager $nucleus.extension.name.camelcase$Manager)
		{
			this.Context = Context;
			this.PageModuleManager = pageModuleManager;
			this.$nucleus.extension.name$Manager = $nucleus.extension.name.camelcase$Manager;			
		}

		[HttpGet]
		[HttpPost]
		public ActionResult Settings(ViewModels.Settings viewModel)
		{
			return View("Settings", BuildSettingsViewModel(viewModel));
		}

		[HttpPost]
		public async Task<ActionResult> SaveSettings(ViewModels.Settings viewModel)
		{
      viewModel.SetSettings(this.Context.Module);

      await this.PageModuleManager.SaveSettings(this.Context.Page, this.Context.Module);

			return Ok();
		}

		private ViewModels.Settings BuildSettingsViewModel(ViewModels.Settings viewModel)
		{
			if (viewModel == null)
			{
				viewModel = new();
			}

			viewModel.GetSettings(this.Context.Module);

			return viewModel;
		}
	}
}