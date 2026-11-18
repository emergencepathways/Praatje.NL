using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

class P : ISignatureTypeProvider<string, object> {
  MetadataReader r; public P(MetadataReader r){this.r=r;}
  public string GetArrayType(string e, ArrayShape s)=>e+"[,]";
  public string GetByReferenceType(string e)=>"ref "+e;
  public string GetFunctionPointerType(MethodSignature<string> s)=>"fnptr";
  public string GetGenericInstantiation(string g, ImmutableArray<string> a)=>g+"<"+string.Join(",",a)+">";
  public string GetGenericMethodParameter(object c,int i)=>"!!"+i;
  public string GetGenericTypeParameter(object c,int i)=>"!"+i;
  public string GetModifiedType(string m,string u,bool req)=>u;
  public string GetPinnedType(string e)=>e;
  public string GetPointerType(string e)=>e+"*";
  public string GetPrimitiveType(PrimitiveTypeCode t)=>t.ToString().ToLower();
  public string GetSZArrayType(string e)=>e+"[]";
  public string GetTypeFromDefinition(MetadataReader r,TypeDefinitionHandle h,byte k){var t=r.GetTypeDefinition(h);return r.GetString(t.Name);}
  public string GetTypeFromReference(MetadataReader r,TypeReferenceHandle h,byte k){var t=r.GetTypeReference(h);return r.GetString(t.Name);}
  public string GetTypeFromSpecification(MetadataReader r,object c,TypeSpecificationHandle h,byte k)=>r.GetTypeSpecification(h).DecodeSignature(this,c);
}
class Program{ static void Main(string[] a){
  using var fs=File.OpenRead(a[0]); using var pe=new PEReader(fs); var r=pe.GetMetadataReader(); var p=new P(r);
  var filter=a.Length>1?a[1]:null;
  foreach(var th in r.TypeDefinitions){ var t=r.GetTypeDefinition(th);
    string ns=r.GetString(t.Namespace), n=r.GetString(t.Name);
    var decl=t.GetDeclaringType(); if(!decl.IsNil) n=r.GetString(r.GetTypeDefinition(decl).Name)+"/"+n;
    string full=(ns.Length>0?ns+".":"")+n;
    if(filter!=null && !full.Equals(filter) && !(filter.EndsWith("*") && full.StartsWith(filter.TrimEnd('*')))) continue;
    string bt=""; if(!t.BaseType.IsNil){ try{ bt = t.BaseType.Kind==HandleKind.TypeReference? r.GetString(r.GetTypeReference((TypeReferenceHandle)t.BaseType).Name): t.BaseType.Kind==HandleKind.TypeDefinition? r.GetString(r.GetTypeDefinition((TypeDefinitionHandle)t.BaseType).Name):"spec";}catch{} }
    Console.WriteLine($"TYPE {full} : {bt}");
    foreach(var fh in t.GetFields()){var f=r.GetFieldDefinition(fh); var vis=(f.Attributes&FieldAttributes.FieldAccessMask)==FieldAttributes.Public?"public":"private"; var st=(f.Attributes&FieldAttributes.Static)!=0?" static":"";
      Console.WriteLine($"  F {vis}{st} {f.DecodeSignature(p,null)} {r.GetString(f.Name)}");}
    foreach(var ph in t.GetProperties()){var pr=r.GetPropertyDefinition(ph); Console.WriteLine($"  P {pr.DecodeSignature(p,null).ReturnType} {r.GetString(pr.Name)}");}
    foreach(var mh in t.GetMethods()){var m=r.GetMethodDefinition(mh); var s=m.DecodeSignature(p,null);
      var vis=(m.Attributes&MethodAttributes.MemberAccessMask)==MethodAttributes.Public?"public":"private"; var st=(m.Attributes&MethodAttributes.Static)!=0?" static":"";
      var names=m.GetParameters().Select(x=>r.GetParameter(x)).Where(x=>x.SequenceNumber>0).OrderBy(x=>x.SequenceNumber).Select(x=>r.GetString(x.Name)).ToList();
      var ps=s.ParameterTypes.Select((x,i)=>x+" "+(i<names.Count?names[i]:"")); 
      Console.WriteLine($"  M {vis}{st} {s.ReturnType} {r.GetString(m.Name)}({string.Join(", ",ps)})");}
  }
}}
