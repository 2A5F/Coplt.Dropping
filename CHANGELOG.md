# 0.8.0
- Breaking Changes
  - Switch source package to `RegisterPostInitializationOutput`
  - Required .NET 10 +

# 0.7.0
- Breaking Changes
  - Remove Unmanaged options
  - Add DropFrom to replace Unmanaged options
    - Now default is always call drop (before default is only Dispose)
  - Now always add `?` on reference types
  - Default order changed
    - Now default order is (`Methods` decl order **Asc**) -> (`Fields | Props` decl order **Desc**)
    - Explicit marking order still takes precedence

# 0.6.0
- Breaking Changes
    - Switch to the source package, and change the access rights of related classes from public to internal
