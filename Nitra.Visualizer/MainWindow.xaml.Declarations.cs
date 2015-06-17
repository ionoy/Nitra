﻿using Nitra.Declarations;
using Nitra.Runtime.Binding;

using System.Reflection;
using System.Windows.Input;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Nitra.Internal;
using Nitra.ProjectSystem;
using Nitra.Runtime.Reflection;

namespace Nitra.Visualizer
{
  public partial class MainWindow
  {
    private IAst _astRoot;

    private TreeViewItem ObjectToItem(PropertyInfo prop, object obj)
    {
      string name = prop == null ? "" : prop.Name;
      var tvi = new TreeViewItem { Tag = obj, FontWeight = FontWeights.Normal };
      tvi.MouseDoubleClick += TviOnMouseDoubleClick;
      tvi.KeyDown += TviOnKeyDown;
      tvi.Expanded += TviOnExpanded;

      var list = obj as IAstList<IAst>;
      if (list != null)
      {
        var xaml = RenderXamlForlist(name, list);
        tvi.Header = XamlReader.Parse(xaml);
        if (list.Count > 0)
          tvi.Items.Add(obj);
        return tvi;
      }

      var option = obj as IAstOption<IAst>;
      if (option != null)
      {
        if (option.HasValue)
          tvi = ObjectToItem(prop, option.Value);
        else
        {
          var xaml = RenderXamlForValue(prop, "&lt;None&gt;");
          tvi.Header = XamlReader.Parse(xaml);
        }
        return tvi;
      }

      var declaration = obj as IAst;
      if (declaration != null)
      {
        var xaml   = RenderXamlForDeclaration(name, declaration);
        tvi.Header = XamlReader.Parse(xaml);
        var t      = obj.GetType();
        var props  = t.GetProperties();

        if (props.Any(p => !IsIgnoredProperty(p)))
          tvi.Items.Add(obj);
        
        return tvi;
      }

      var items = obj as IEnumerable;
      if (items != null && !(items is string))
      {
        var type = items.GetType();
        var count = items.Count();
        var xaml = RenderXamlForSeq(name, count);
        tvi.Header = XamlReader.Parse(xaml);
        if (count > 0)
          tvi.Items.Add(obj);
        return tvi;
      }
      else
      {
        var xaml = RenderXamlForValue(prop, obj);
        tvi.Header = XamlReader.Parse(xaml);

        if (obj == null)
          return tvi;

        var t = obj.GetType();
        var props = t.GetProperties();
        if (!(obj is string || t.IsPrimitive) && props.Any(p => !IsIgnoredProperty(p)))
          tvi.Items.Add(obj);

        return tvi;
      }
    }

    private void TviOnExpanded(object sender, RoutedEventArgs routedEventArgs)
    {
      routedEventArgs.Handled = true;

      var tvi = (TreeViewItem)sender;
      TviExpanded(tvi);
    }

    private void TviExpanded(TreeViewItem tvi)
    {
      var obj = tvi.Tag;
      tvi.Items.Clear();

      var list = obj as IAstList<IAst>;
      if (list != null)
      {
        foreach (var item in list)
          tvi.Items.Add(ObjectToItem(null, item));
        return;
      }

      if (obj is IAstOption<IAst>)
        return;

      var declaration = obj as IAst;
      if (declaration != null)
      {
        var t = obj.GetType();
        var props = t.GetProperties();

        foreach (var prop in props) //.OrderBy(p => p.Name))
        {
          if (IsIgnoredProperty(prop))
            continue;
          try
          {
            if (declaration.IsMissing)
              return;
            var isEvalPropName = "Is" + prop.Name + "Evaluated";
            var isEvalProp = t.GetProperty(isEvalPropName);
            if (isEvalProp == null || (bool) isEvalProp.GetValue(declaration, null))
            {
              var value = prop.GetValue(declaration, null);
              tvi.Items.Add(ObjectToItem(prop, value));
            }
            else
            {
              var tviNotEval = ObjectToItem(prop, "<not evaluated>");
              tviNotEval.Foreground = Brushes.Red;
              tviNotEval.FontWeight = FontWeights.Bold;
              tvi.Items.Add(tviNotEval);
            }
          }
          catch (Exception e)
          {
            tvi.Items.Add(ObjectToItem(prop, e.Message));
          }
        }
        return;
      }

      var items = obj as IEnumerable;
      if (items != null && !(items is string))
      {
        foreach (var item in (IEnumerable) obj)
          tvi.Items.Add(ObjectToItem(null, item));
        return;
      }

      {
        var t = obj.GetType();

        if (obj is string || t.IsPrimitive)
          return;

        var props = t.GetProperties();

        foreach (var prop in props) //.OrderBy(p => p.Name))
        {
          if (IsIgnoredProperty(prop))
            continue;
          try
          {
            var value = prop.GetValue(obj, null);
            tvi.Items.Add(ObjectToItem(prop, value));
          }
          catch (Exception e)
          {
            tvi.Items.Add(ObjectToItem(prop, e.Message));
          }
        }
      }
    }

    private static bool IsIgnoredProperty(PropertyInfo prop)
    {
      switch (prop.Name)
      {
        case "HasValue":
          return false;
        case "Id":
        case "IsMissing":
        case "File":
        case "Span":
        case "IsAmbiguous":
          return true;
      }
      return false;
    }

    private void UpdateDeclarations()
    {
      if (_astRoot == null)
        return;

      _declarationsTreeView.Items.Clear();

      var rootTreeViewItem = ObjectToItem(null, _astRoot);
      rootTreeViewItem.Header = "Root";
      _declarationsTreeView.Items.Add(rootTreeViewItem);
    }

    private static string RenderXamlForDeclaration(string name, IAst ast)
    {
      var declatation = ast as IDeclaration;
      var suffix = declatation == null ? null : (": " + Utils.Escape(declatation.Name.Text));
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
" + (string.IsNullOrWhiteSpace(name) ? null : ("<Span Foreground = 'blue'>" + Utils.Escape(name) + "</Span>: "))
             + ast.ToXaml() + suffix + @"
</Span>";
    }

    private static string RenderXamlForValue(PropertyInfo prop, object obj)
    {
      if (obj == null)
        obj = "<null>";
      var isDependent = prop != null && prop.IsDefined(typeof(DependentPropertyAttribute), false);
      var color = isDependent ? "green" : "SlateBlue";
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
" + (prop == null ? null : ("<Bold><Span Foreground = '" + color + "'>" + Utils.Escape(prop.Name) + "</Span></Bold>: "))
             + Utils.Escape(obj.ToString()) + @"
</Span>";
    }

    private static string RenderXamlForlist(string name, IAstList<IAst> items)
    {
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
<Span Foreground = 'blue'>" + Utils.Escape(name) + @"</Span>* <Span Foreground = 'gray'>Count: </Span> " + items.Count + @"
</Span>";
    }

    private static string RenderXamlForSeq(string name, int count)
    {
      return @"
<Span xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
<Span Foreground = 'green'>" + Utils.Escape(name) + @"</Span> <Span Foreground = 'gray'>(List) Count: </Span> " + count + @"
</Span>";
    }

    private void _declarationsTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
      if (e.NewValue != null)
      {
        var obj = ((TreeViewItem) e.NewValue).Tag;
        var symbol = obj as Symbol2;
        var id = symbol != null ? symbol.Id : (obj == null ? 0 : obj.GetHashCode());
        try
        {
          _propertyGrid.SelectedObject = obj;
        }
        catch
        {
        }
        _objectType.Text = obj == null ? "<null>" : obj.GetType().FullName + " [" + id + "]";
      }
    }

    private void TviOnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      SelectCodeForDeclarationPart(sender);
      e.Handled = true;
    }

    private void SelectCodeForDeclarationPart(object sender)
    {
      var tvi = (TreeViewItem) sender;
      if (!tvi.IsSelected)
        return;

      var ast = tvi.Tag as IAst;
      if (ast != null)
        SelectText(ast);

      TrySelectTextForSymbol(tvi.Tag as Symbol2, tvi);
    }

    private void TrySelectTextForSymbol(Symbol2 symbol, TreeViewItem tvi)
    {
      if (symbol != null && !symbol.Declarations.IsEmpty)
      {
        if (symbol.Declarations.Length == 1)
          SelectText(symbol.Declarations.Head);
        else
        {
          if (!tvi.IsExpanded)
            tvi.IsExpanded = true;
          foreach (TreeViewItem subItem in tvi.Items)
          {
            var decls = subItem.Tag as Nemerle.Core.list<Nitra.Declarations.IDeclaration>;
            if (decls != null)
            {
              subItem.IsExpanded = true;
              subItem.IsSelected = true;

              foreach (TreeViewItem subSubItem in tvi.Items)
                subSubItem.BringIntoView();

              break;
            }
          }
        }
      }
    }

    private void SelectText(IAst ast)
    {
      SelectText(ast.File, ast.Span);
    }

    private void SelectText(Location loc)
    {
      SelectText(loc.Source.File, loc.Span);
    }

    private void SelectText(File file, NSpan span)
    {
      if (_currentTestFolder != null)
      {
        foreach (var test in _currentTestFolder.Tests)
        {
          if (test.File == file)
          {
            test.IsSelected = true;
            break;
          }
        }
      }
      _text.CaretOffset = span.StartPos;
      _text.Select(span.StartPos, span.Length);
      _text.ScrollTo(_text.TextArea.Caret.Line, _text.TextArea.Caret.Column);
    }

    private void TviOnKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Return)
      {
        SelectCodeForDeclarationPart(sender);
        e.Handled = true;
      }
    }
  }
}
