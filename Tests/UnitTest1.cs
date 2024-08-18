using Coplt.Dropping;

namespace Tests;

[Dropping]
public partial class Foo1
{
    [Drop]
    public void Drop()
    {
        Console.WriteLine(1);
    }
}

[Dropping(Unmanaged = true)]
public partial class Foo2
{
    [Drop]
    private void Drop()
    {
        Console.WriteLine(1);
    }

    [Drop(Unmanaged = false)]
    private void Drop2()
    {
        Console.WriteLine(2);
    }
}

[Dropping]
public partial class Foo3 : Foo2
{
    [Drop]
    private void Drop()
    {
        Console.WriteLine(1);
    }

    [Drop(Unmanaged = true)]
    private void Drop2()
    {
        Console.WriteLine(2);
    }
}

[Dropping]
public sealed partial class Foo4
{
    [Drop]
    public void Drop()
    {
        Console.WriteLine(1);
    }
}

[Dropping(Unmanaged = true)]
public sealed partial class Foo5
{
    [Drop]
    public void Drop()
    {
        Console.WriteLine(1);
    }
}

public class SomeBase : IDisposable
{
    protected virtual void Dispose(bool disposing) { }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

[Dropping(Unmanaged = true)]
public sealed partial class Foo6 : SomeBase
{
     [Drop]
     private void Drop()
     {
         Console.WriteLine(1);
     }

     [Drop(Unmanaged = false)]
     private void Drop2()
     {
         Console.WriteLine(2);
     }
}

[Dropping(Unmanaged = true)]
public partial struct Foo7
{
    [Drop]
    private void Drop()
    {
        Console.WriteLine(1);
    }

    [Drop(Unmanaged = false)]
    private void Drop2()
    {
        Console.WriteLine(2);
    }
}

[Dropping]
public partial struct Foo8
{
    [Drop]
    private void Drop()
    {
        Console.WriteLine(1);
    }
}

[Dropping]
public partial class Foo9
{
    [Drop]
    private void Drop(bool disposing)
    {
        Console.WriteLine(disposing);
    }
}

[Dropping]
public partial struct Foo10
{
    [Drop]
    private void Drop(bool disposing)
    {
        Console.WriteLine(disposing);
    }
}

[Dropping]
public partial class Foo11 : SomeBase
{
    [Drop]
    private void Drop(bool disposing)
    {
        Console.WriteLine(disposing);
    }
}

[Dropping(Unmanaged = true)]
public sealed partial class Foo12
{
    [Drop]
    private void Drop(bool disposing)
    {
        Console.WriteLine(disposing);
    }
}


[Dropping(Unmanaged = true)]
public sealed partial class Foo13
{
    [Drop]
    public Foo1? a;
    public int b;
    
    [Drop]
    private void Drop(bool disposing)
    {
        Console.WriteLine(disposing);
    }
}

[Dropping(Unmanaged = true)]
public sealed partial class Foo14
{
    [Drop]
    public Foo8? a;
    public int b;
    
    [Drop]
    private void Drop(bool disposing)
    {
        Console.WriteLine(disposing);
    }
}

[Dropping]
public partial struct Foo15<T> where T : IDisposable
{
    [Drop]
    public T a;
    
    [Drop]
    private void Drop()
    {
        Console.WriteLine(1);
    }
}

[Dropping]
public partial struct Foo16<T> where T : IDisposable
{
    [Drop]
    public T? a;
    
    [Drop]
    private void Drop()
    {
        Console.WriteLine(1);
    }
}

public class Tests
{
    [SetUp]
    public void Setup() { }

    [Test]
    public void Test1()
    {
        Assert.Pass();
    }
}
