# AGENTS Guidelines

This project targets **.NET 9**.  

---

## Documentation  
- All classes and methods **must be documented in English**, including private ones.  
- Methods that handle **data streams** or **binary data transformations** must include clear comments.  

---

## Design Principles  
- Follow the **separation of concerns** principle:  
  - **Data classes** should only hold data.  
  - **Processing classes** should contain logic.  
- Processing logic should rely on **interfaces**, including **generic interfaces** where appropriate.  

---

## Testing  
- Every change must include a corresponding **test**.  
- The only exception is when modifying **library metadata**.  

---

## README  
- The project’s **README.md** must include an **example snippet**.  

---

## Coding Standards  
- Arrays must use **bracket syntax** (`[]`).  
- If a method uses `params` and elements are read sequentially, prefer `params IEnumerable<T>`.  
- File-reading methods must **only open the file** and then delegate content processing to a dedicated method.  
- Large `switch` statements (more than **10 cases** or **30 lines**) must be replaced by either:  
  - `Dictionary<case, method>` (each method handling one case), or  
  - `Dictionary<case, class>` depending on code complexity.  
- Code indentation must use **spaces, 4 per level**.  

---
