# Run using the CODESYS Scripting engine. No PLC connection or download is made.
# Native application import preserves the operator panel, not personal device settings.
import os, io
from scriptengine import *

folder = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(os.path.dirname(folder))
source = os.path.join(root, 'simulasyon_unity', 'plc')
target = os.environ.get('RVM_CODESYS_OUTPUT', os.path.join(folder, 'RVM_Unity_Dijital_Ikiz_v106.project'))
version = os.environ.get('RVM_DEVICE_VERSION', '3.5.22.30')

def read(name):
    with io.open(os.path.join(source, name + '.st'), encoding='utf-8-sig') as f:
        return f.read()

p = None
try:
    if os.path.exists(target):
        raise RuntimeError('Output already exists. Set RVM_CODESYS_OUTPUT to a new project path.')
    devices = [d for d in device_repository.get_all_devices('CODESYS Control Win V3 x64') if str(d.device_id.version) == version]
    if not devices:
        raise RuntimeError('Install Control Win V3 x64 ' + version + ' or set RVM_DEVICE_VERSION.')
    p = projects.create(target)
    p.add('Device', devices[0].device_id)
    blank = p.find('Application', True)[0]
    logic = blank.parent
    blank.remove()
    logic.import_native(filenames=[os.path.join(folder, 'Application_v106.export')])
    app = p.find('Application', True)[0]
    # A fresh project uses default visualization property handling. The panel
    # binds plain GVL variables; remove the old project's property-info define.
    definitions = app.build_properties.compiler_defines
    if isinstance(definitions, basestring):
        app.build_properties.compiler_defines = definitions.replace('VISU_USEPROPERTYINFO', '')
    else:
        app.build_properties.compiler_defines = [d for d in definitions if d != 'VISU_USEPROPERTYINFO']
    for name in ['ST_RVMInput106', 'ST_RVMOutput106', 'ST_RVMOperator106', 'GVL_RVMUnity', 'GVL_RVMOperator', 'GVL_RVMKalici']:
        app.find(name, False)[0].textual_declaration.replace(read(name))
    for name in ['F_RVMPriority106', 'F_RVMMaterial106', 'FB_RVMControl106', 'PRG_UnityRVM']:
        obj = app.find(name, False)[0]
        obj.textual_declaration.replace(read(name + '_decl'))
        obj.textual_implementation.replace(read(name + '_body'))
    task = p.find('AnaGorev', True)[0]
    if [task.pous[i][0] for i in range(len(task.pous))] != ['PRG_UnityRVM']:
        raise RuntimeError('Unexpected automation task calls.')
    task.interval = 't#20ms'
    p.save()
    app.rebuild()
    app.generate_code()
    messages = []
    for category in system.get_message_categories():
        for message in system.get_messages(category): messages.append(unicode(message))
    with io.open(os.path.join(folder, 'build.log'), 'w', encoding='utf-8') as f:
        f.write(u'\n'.join(messages))
    if not any('Build complete -- 0 errors' in m for m in messages):
        raise RuntimeError('Build failed. See build.log.')
    p.save()
    app.export_xml(path=os.path.join(folder, 'RVM_Unity_Dijital_Ikiz_v106.plcopen.xml'), recursive=True, declarations_as_plaintext=True)
finally:
    if p is not None: p.close()
