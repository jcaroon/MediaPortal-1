#region Copyright (C) 2007-2008 Team MediaPortal

/*
    Copyright (C) 2007-2008 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using Presentation.SkinEngine.General;
using SlimDX;
using MediaPortal.Presentation.Properties;
using MediaPortal.Control.InputManager;
using Presentation.SkinEngine.Controls.Visuals.Triggers;
using Presentation.SkinEngine.Controls.Animations;
using Presentation.SkinEngine.Controls.Transforms;
using Presentation.SkinEngine.Commands;
using Presentation.SkinEngine.MpfElements.Resources;
using Presentation.SkinEngine.XamlParser.Interfaces;
using Presentation.SkinEngine.Controls.Panels;
using MediaPortal.Utilities.DeepCopy;
using Presentation.SkinEngine.SkinManagement;

namespace Presentation.SkinEngine.Controls.Visuals
{

  public enum VisibilityEnum
  {
    Visible = 0,
    Hidden = 1,
    Collapsed = 2,
  }

  public enum UIEvent : int
  {
    None = 0,
    Hidden = 1,
    Visible = 2,
    OpacityChange = 4,
    StrokeChange=8,
    FillChange=16,
  }

  public class ZOrderComparer : IComparer<UIElement>
  {
    #region IComparer<UIElement> Members

    public int Compare(UIElement x, UIElement y)
    {
      return Panel.GetZIndex(x).CompareTo(Panel.GetZIndex(y));
    }

    #endregion
  }

  /// <summary>
  /// Delegate interface which decides if an element fulfills a special condition.
  /// </summary>
  public interface IFinder
  {
    /// <summary>
    /// Query method which decides if the specified <paramref name="current"/>
    /// element fulfills the condition exposed by this class.
    /// </summary>
    /// <returns><c>true</c> if the specified <paramref name="current"/> element
    /// fulfills the condition exposed by this class, else <c>false</c>.</returns>
    bool Query(UIElement current);
  }

  /// <summary>
  /// Finder implementation which looks for elements of the specified type.
  /// </summary>
  public class TypeFinder : IFinder
  {
    protected Type _type;

    public TypeFinder(Type type)
    {
      _type = type;
    }

    public bool Query(UIElement current)
    {
      return _type == current.GetType();
    }
  }

  /// <summary>
  /// Finder implementation which looks for elements of a the given type or
  /// of a type derived from the given type.
  /// </summary>
  public class SubTypeFinder : IFinder
  {
    protected Type _type;

    public SubTypeFinder(Type type)
    {
      _type = type;
    }

    public bool Query(UIElement current)
    {
      return _type.IsAssignableFrom(current.GetType());
    }
  }

  /// <summary>
  /// Finder implementation which looks for elements of a specified name.
  /// </summary>
  public class NameFinder : IFinder
  {
    protected string _name;

    public NameFinder(string name)
    {
      _name = name;
    }

    public bool Query(UIElement current)
    {
      return _name == current.Name;
    }
  }

  /// <summary>
  /// Finder implementation which looks for focused elements.
  /// </summary>
  public class FocusFinder : IFinder
  {
    private static FocusFinder _instance = null;

    public bool Query(UIElement current)
    {
      return current.HasFocus;
    }

    public static FocusFinder Instance
    {
      get
      {
        if (_instance == null)
          _instance = new FocusFinder();
        return _instance;
      }
    }
  }

  public class UIElement : Visual, IContentEnabled
  {
    #region Private/protected fields

    Property _nameProperty;
    Property _focusableProperty;
    Property _isFocusScopeProperty;
    Property _hasFocusProperty;
    Property _acutalPositionProperty;
    Property _marginProperty;
    Property _triggerProperty;
    Property _renderTransformProperty;
    Property _renderTransformOriginProperty;
    Property _layoutTransformProperty;
    Property _visibilityProperty;
    Property _isEnabledProperty;
    Property _opacityMaskProperty;
    Property _opacityProperty;
    Property _freezableProperty;
    Property _isTemplateRootProperty;
    protected SizeF _desiredSize;
    protected SizeF _availableSize;
    protected RectangleF _finalRect;
    bool _isArrangeValid;
    bool _isTemplateRoot;
    ResourceDictionary _resources;
    protected bool _isLayoutInvalid = true;
    protected ExtendedMatrix _finalLayoutTransform;
    IExecutableCommand _loaded;
    bool _triggersInitialized;
    bool _fireLoaded = true;
    bool _isVisible = true;

    #endregion

    #region Ctor

    public UIElement()
    {
      Init();
      Attach();
    }

    void Init()
    {
      _nameProperty = new Property(typeof(string), "");
      _focusableProperty = new Property(typeof(bool), false);
      _isFocusScopeProperty = new Property(typeof(bool), true);
      _hasFocusProperty = new Property(typeof(bool), false);
      _acutalPositionProperty = new Property(typeof(Vector3), new Vector3(0, 0, 1));
      _marginProperty = new Property(typeof(Thickness), new Thickness(0, 0, 0, 0));
      _resources = new ResourceDictionary();
      _triggerProperty = new Property(typeof(IList<TriggerBase>), new List<TriggerBase>());
      _renderTransformProperty = new Property(typeof(Transform), null);
      _layoutTransformProperty = new Property(typeof(Transform), null);
      _renderTransformOriginProperty = new Property(typeof(Vector2), new Vector2(0, 0));
      _visibilityProperty = new Property(typeof(VisibilityEnum), VisibilityEnum.Visible);
      _isEnabledProperty = new Property(typeof(bool), true);
      _freezableProperty = new Property(typeof(bool), false);
      _opacityProperty = new Property(typeof(double), 1.0);

      _opacityMaskProperty = new Property(typeof(Brushes.Brush), null);
      _isTemplateRootProperty = new Property(typeof(bool), false);
    }

    void Attach()
    {
      _marginProperty.Attach(OnPropertyChanged);
      _visibilityProperty.Attach(OnVisibilityPropertyChanged);
      _opacityProperty.Attach(OnOpacityPropertyChanged);
      _hasFocusProperty.Attach(OnFocusPropertyChanged);
    }

    void Detach()
    {
      _marginProperty.Detach(OnPropertyChanged);
      _visibilityProperty.Detach(OnVisibilityPropertyChanged);
      _opacityProperty.Detach(OnOpacityPropertyChanged);
      _hasFocusProperty.Detach(OnFocusPropertyChanged);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      Detach();
      base.DeepCopy(source, copyManager);
      UIElement el = source as UIElement;
      Name = copyManager.GetCopy(el.Name);
      Focusable = copyManager.GetCopy(el.Focusable);
      IsFocusScope = copyManager.GetCopy(el.IsFocusScope);
      // We do not copy the focus flag, only one element can have focus
      //HasFocus = copyManager.GetCopy(el.HasFocus);
      ActualPosition = copyManager.GetCopy(el.ActualPosition);
      Margin = copyManager.GetCopy(el.Margin);
      Visibility = copyManager.GetCopy(el.Visibility);
      IsEnabled = copyManager.GetCopy(el.IsEnabled);
      // FIXME Albert78: Implement Freezing
      Freezable = copyManager.GetCopy(el.Freezable);
      Opacity = copyManager.GetCopy(el.Opacity);
      Loaded = copyManager.GetCopy(el.Loaded);
      OpacityMask = copyManager.GetCopy(el.OpacityMask);
      LayoutTransform = copyManager.GetCopy(el.LayoutTransform);
      RenderTransform = copyManager.GetCopy(el.RenderTransform);
      RenderTransformOrigin = copyManager.GetCopy(el.RenderTransformOrigin);
      IsTemplateRoot = copyManager.GetCopy(el.IsTemplateRoot);
      // Simply reuse the Resources
      SetResources(el._resources);

      foreach (TriggerBase t in el.Triggers)
        Triggers.Add(copyManager.GetCopy(t));
      Attach();
    }

    #endregion

    void OnOpacityPropertyChanged(Property property)
    {
      FireUIEvent(UIEvent.OpacityChange, this);
    }

    void OnVisibilityPropertyChanged(Property property)
    {
      if (VisualParent is UIElement)
      {
        ((UIElement) VisualParent).Invalidate();
      }
      _isVisible = (Visibility == VisibilityEnum.Visible);
      if (!_isVisible)
        FireUIEvent(UIEvent.Hidden, this);
      else
        FireUIEvent(UIEvent.Visible, this);
    }

    void OnFocusPropertyChanged(Property property)
    {
      if (HasFocus)
      {
        FocusManager.FocusedElement = this;
        FireEvent("OnGotFocus");
      }
      else
        FireEvent("OnLostFocus");
    }

    public virtual void FireUIEvent(UIEvent eventType, UIElement source)
    { }

    /// <summary>
    /// Called when a property value has been changed
    /// Since all UIElement properties are layout properties
    /// we're simply calling Invalidate() here to invalidate the layout
    /// </summary>
    /// <param name="property">The property.</param>
    void OnPropertyChanged(Property property)
    {
      Invalidate();
    }

    public void SetResources(ResourceDictionary resources)
    {
      _resources = resources;
    }

    #region Public properties

    public IExecutableCommand Loaded
    {
      get { return _loaded; }
      set { _loaded = value; }
    }

    public ResourceDictionary Resources
    {
      get { return _resources; }
    }

    public ExtendedMatrix FinalLayoutTransform
    {
      get { return _finalLayoutTransform; }
    }

    public Property OpacityProperty
    {
      get { return _opacityProperty; }
    }

    public double Opacity
    {
      get { return (double) _opacityProperty.GetValue(); }
      set { _opacityProperty.SetValue(value); }
    }

    public Property FreezableProperty
    {
      get { return _freezableProperty; }
    }

    public bool Freezable
    {
      get { return (bool)_freezableProperty.GetValue(); }
      set { _freezableProperty.SetValue(value); }
    }

    public Property OpacityMaskProperty
    {
      get { return _opacityMaskProperty; }
    }

    public Brushes.Brush OpacityMask
    {
      get { return (Brushes.Brush) _opacityMaskProperty.GetValue(); }
      set { _opacityMaskProperty.SetValue(value); }
    }

    public Property IsEnabledProperty
    {
      get { return _isEnabledProperty; }
    }

    public bool IsEnabled
    {
      get { return (bool)_isEnabledProperty.GetValue(); }
      set { _isEnabledProperty.SetValue(value); }
    }

    public Property VisibilityProperty
    {
      get { return _visibilityProperty; }
    }

    public VisibilityEnum Visibility
    {
      get { return (VisibilityEnum)_visibilityProperty.GetValue(); }
      set
      {
        //Trace.WriteLine(String.Format("set {0} :{1}", this.Name, value));
        _visibilityProperty.SetValue(value);
      }
    }

    public Property TriggersProperty
    {
      get { return _triggerProperty; }
    }

    public IList<TriggerBase> Triggers
    {
      get { return (IList<TriggerBase>)_triggerProperty.GetValue(); }
    }

    public Property ActualPositionProperty
    {
      get { return _acutalPositionProperty; }
    }

    public Vector3 ActualPosition
    {
      get { return (Vector3) _acutalPositionProperty.GetValue(); }
      set { _acutalPositionProperty.SetValue(value); }
    }

    public Property NameProperty
    {
      get { return _nameProperty; }
    }

    public string Name
    {
      get { return _nameProperty.GetValue() as string; }
      set
      {
        INameScope ns = FindNameScope();
        if (ns != null)
          ns.UnregisterName(Name);
        _nameProperty.SetValue(value);
        if (ns != null)
          try
          {
            if (ns.FindName(Name) == this)
              return; // Avoid exception when registered multiple times
            ns.RegisterName(Name, this);
          }
          catch
          {
            throw new ArgumentException("Name '"+Name+"' was registered twice in namescope '"+ns.ToString()+"'");
          }
      }
    }

    public Property HasFocusProperty
    {
      get { return _hasFocusProperty; }
    }

    public virtual bool HasFocus
    {
      get { return (bool)_hasFocusProperty.GetValue(); }
      set { _hasFocusProperty.SetValue(value); }
    }

    public Property FocusableProperty
    {
      get { return _focusableProperty; }
    }

    public bool Focusable
    {
      get { return (bool)_focusableProperty.GetValue(); }
      set { _focusableProperty.SetValue(value); }
    }

    public Property IsFocusScopeProperty
    {
      get { return _isFocusScopeProperty; }
    }

    public bool IsFocusScope
    {
      get { return (bool)_isFocusScopeProperty.GetValue(); }
      set { _isFocusScopeProperty.SetValue(value); }
    }

    public bool IsVisible
    {
      get { return _isVisible; }
      set
      {
        if (value)
          Visibility = VisibilityEnum.Visible;
        else
          Visibility = VisibilityEnum.Hidden;
      }
    }

    public Property MarginProperty
    {
      get { return _marginProperty; }
    }

    public Thickness Margin
    {
      get { return (Thickness)_marginProperty.GetValue(); }
      set { _marginProperty.SetValue(value); }
    }

    public Property LayoutTransformProperty
    {
      get { return _layoutTransformProperty; }
    }

    public Transform LayoutTransform
    {
      get { return _layoutTransformProperty.GetValue() as Transform; }
      set { _layoutTransformProperty.SetValue(value); }
    }

    public Property RenderTransformProperty
    {
      get { return _renderTransformProperty; }
    }

    public Transform RenderTransform
    {
      get { return (Transform) _renderTransformProperty.GetValue(); }
      set { _renderTransformProperty.SetValue(value); }
    }

    public Property RenderTransformOriginProperty
    {
      get { return _renderTransformOriginProperty; }
    }

    public Vector2 RenderTransformOrigin
    {
      get { return (Vector2) _renderTransformOriginProperty.GetValue(); }
      set { _renderTransformOriginProperty.SetValue(value); }
    }

    public bool IsArrangeValid
    {
      get {
        // FIXME Albert78: This variable avoids the Invalidate call to set the _isLayoutInvalid variable
        // What is the meaning of this property??? If it is not necessary, remove it
        return true;// _isArrangeValid;
      }
      set { _isArrangeValid = value; }
    }

    public SizeF DesiredSize
    {
      get { return _desiredSize; }
    }

    public Property IsTemplateRootProperty
    {
      get { return _isTemplateRootProperty; }
    }

    public bool IsTemplateRoot
    {
      get { return (bool) _isTemplateRootProperty.GetValue(); }
      set { _isTemplateRootProperty.SetValue(value); }
    }

    #endregion

    public virtual void GetSizeForBrush(out double width, out double height)
    {
      width = 0.0;
      height = 0.0;
    }

    /// <summary>
    /// Will measure this element according to the <paramref name="availableSize"/>
    /// given. This method will fill the <see cref="DesiredSize"/> property.
    /// </summary>
    /// <param name="availableSize">Maximum size which is available for this element.</param>
    public virtual void Measure(SizeF availableSize)
    {
      _availableSize = new SizeF(availableSize.Width, availableSize.Height);
    }

    /// <summary>
    /// Arranges the UI element and positions it in the finalrect.
    /// </summary>
    /// <param name="finalRect">The final size that the parent computes for the child element.</param>
    public virtual void Arrange(RectangleF finalRect)
    {
      IsArrangeValid = true;
      Initialize();
      InitializeTriggers();
      _isLayoutInvalid = false;
    }

    /// <summary>
    /// Invalidates the layout of this UIElement.
    /// If dimensions change, it will invalidate the parent visual so the parent
    /// will re-layout itself and its children
    /// </summary>
    public virtual void Invalidate()
    {
      if (!IsArrangeValid) return; // FIXME Albert78: Why this line??????
      _isLayoutInvalid = true;
      if (VisualParent == null)
        _availableSize = new SizeF(0, 0);
    }

    /// <summary>
    /// Updates the layout.
    /// </summary>
    public virtual void UpdateLayout()
    {
      if (false == _isLayoutInvalid) return;
      //Trace.WriteLine("UpdateLayout :" + this.Name + "  " + this.GetType());
      _isLayoutInvalid = false;
      ExtendedMatrix m = _finalLayoutTransform;
      if (_availableSize.Width > 0 && _availableSize.Height > 0)
      {
        SizeF sizeOld = new SizeF(_desiredSize.Width, _desiredSize.Height);
        SizeF availsizeOld = new SizeF(_availableSize.Width, _availableSize.Height);
        if (m != null)
          SkinContext.AddLayoutTransform(m);
        Measure(_availableSize);
        if (m != null)
          SkinContext.RemoveLayoutTransform();
        _availableSize = availsizeOld;
        if (_desiredSize == sizeOld)
        {
          if (m != null)
            SkinContext.AddLayoutTransform(m);
          Arrange(_finalRect);
          if (m != null)
            SkinContext.RemoveLayoutTransform();
          return;
        }
      }
      if (VisualParent is UIElement)
      {
        ((UIElement) VisualParent).Invalidate();
        ((UIElement) VisualParent).UpdateLayout();
      }
      else
      {
        FrameworkElement element = this as FrameworkElement;
        if (element == null)
        {
          SizeF size = new SizeF(SkinContext.SkinWidth * SkinContext.Zoom.Width, SkinContext.SkinHeight * SkinContext.Zoom.Height);
          Measure(size);
          Arrange(new RectangleF(0, 0, size.Width, size.Height));
        }
        else
        {
          float w = (float)element.Width * SkinContext.Zoom.Width;
          float h = (float)element.Height * SkinContext.Zoom.Height;
          if (w == 0) w = SkinContext.SkinWidth * SkinContext.Zoom.Width;
          if (h == 0) h = SkinContext.SkinHeight * SkinContext.Zoom.Height;
          if (m != null)
            SkinContext.AddLayoutTransform(m);
          Measure(new SizeF(w, h));
          if (m != null)
            SkinContext.RemoveLayoutTransform();

          if (m != null)
            SkinContext.AddLayoutTransform(m);
          Arrange(new RectangleF((float)Canvas.GetLeft(element) * SkinContext.Zoom.Width,
            (float)Canvas.GetTop(element) * SkinContext.Zoom.Height, w, h));
          if (m != null)
            SkinContext.RemoveLayoutTransform();
        }
      }
    }

    /// <summary>
    /// Finds the resource with the given resource key.
    /// </summary>
    /// <param name="resourceKey">The resource key.</param>
    /// <returns>Resource with the specified key, or <c>null</c> if not found.</returns>
    public object FindResource(string resourceKey)
    {
      if (Resources.ContainsKey(resourceKey))
      {
        return Resources[resourceKey];
      }
      else if (LogicalParent is UIElement)
      {
        return ((UIElement)LogicalParent).FindResource(resourceKey);
      }
      else
        return SkinContext.SkinResources.FindStyleResource(resourceKey);
    }

    public void InitializeTriggers()
    {
      if (!_triggersInitialized)
      {
        _triggersInitialized = true;
        foreach (TriggerBase trigger in Triggers)
          trigger.Setup(this);
      }
    }

    /// <summary>
    /// Fires an event.
    /// </summary>
    /// <param name="eventName">Name of the event.</param>
    public virtual void FireEvent(string eventName)
    {
      foreach (TriggerBase trigger in Triggers)
      {
        EventTrigger eventTrig = trigger as EventTrigger;
        if (eventTrig != null)
          if (eventTrig.RoutedEvent == eventName)
            foreach (TriggerAction ta in eventTrig.Actions)
              ta.Execute(this, eventTrig);
      }
      if (eventName == "FrameworkElement.Loaded")
        if (Loaded != null)
          _loaded.Execute();
    }

    public void StartStoryboard(Storyboard board)
    {
      Window.Animator.StartStoryboard(board, this);
    }

    public void StopStoryboard(Storyboard board)
    {
      Window.Animator.StopStoryboard(board, this);
    }

    /// <summary>
    /// Will be called when the mouse moves.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    public virtual void OnMouseMove(float x, float y)
    { }

    /// <summary>
    /// Will be called when a key is pressed. Derived classes may override this method
    /// to implement special key handling code.
    /// </summary>
    /// <param name="key">The key.</param>
    public virtual void OnKeyPressed(ref Key key)
    { }

    public virtual bool ReplaceElementType(Type t, UIElement newElement)
    {
      return false;
    }

    /// <summary>
    /// Steps the structure down (in direction to the child elements) along
    /// the visual tree to find an <see cref="UIElement"/> which fulfills the
    /// condition specified by <paramref name="finder"/>. This method has to be
    /// overridden in all descendants exposing visual children, for every child
    /// this method has to be called.
    /// For performance reasons, this method does a depth-first search.
    /// </summary>
    /// <param name="finder">Callback interface which decides if an element
    /// is the element searched for.</param>
    /// <returns><see cref="UIElement"/> for which the specified
    /// <paramref name="finder"/> delegate returned <c>true</c>.</returns>
    public virtual UIElement FindElement(IFinder finder)
    {
      if (finder.Query(this))
        return this;
      else
        return null;
    }

    #region IContentEnabled members

    public bool FindContentProperty(out IDataDescriptor dd)
    {
      return ReflectionHelper.FindPropertyDescriptor(this, "Content", out dd);
    }

    #endregion

    public virtual void Initialize()
    {
      if (_fireLoaded)
      {
        FireEvent("FrameworkElement.Loaded");
        _fireLoaded = false;
      }
    }

    /// <summary>
    /// Resets this object to a state like it were created. This will release all lazy initialized
    /// values and force it to re-initialize. The LogicalTree will remain like it is.
    /// </summary>
    public virtual void Reset()
    {
      _fireLoaded = true;
    }

    public virtual void Allocate()
    {
    }

    public virtual void Deallocate()
    {
    }

    public virtual void SetWindow(Window window)
    {
      Window = window;
    }
  }
}
