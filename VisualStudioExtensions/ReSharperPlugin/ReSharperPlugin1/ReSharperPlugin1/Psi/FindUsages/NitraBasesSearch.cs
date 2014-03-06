using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.ComponentModel;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.ContextNavigation.ContextSearches.BaseSearches;
using JetBrains.ReSharper.Psi;
using JetBrains.Test;

namespace JetBrains.Nitra.FindUsages
{
  [ShellFeaturePart]
  public class NitraBasesSearch : FindUsagesContextSearch
  {
    public NitraBasesSearch()
    {

    }

    private readonly NitraContextSearchImpl mySearch = new NitraContextSearchImpl();

    protected override IList<IDeclaredElement> GetCandidates(IDataContext context)
    {
      return mySearch.GetCandidates(context).Where(element => !(element is NitraDeclaredElement)).ToList();
    }

    public override bool IsContextApplicable(IDataContext dataContext)
    {
      return mySearch.IsContextApplicable(dataContext);
    }
  }
}