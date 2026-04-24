/**
 * 后端服务基础配置
 */
export const BACKEND_URL = 'http://localhost:5246';
export const SIGNALR_HUB_URL = `${BACKEND_URL}/torqueHub`;

/**
 * 默认的应用配置
 */
export const DEFAULT_APP_CONFIG = {
  orderApiUrl: '/mes-api/api/OrderInfo/GetOtherOrderInfoByProcess',
  routeApiUrl: '/mes-api/api/OrderInfo/GetTechRouteListByCode',
  singleMaterialApiUrl: '',
  fullMaterialApiUrl: '',
  codeCreateApiUrl: '',
  mesPushApiUrl: '',
  duplicateCheckApiUrl: '',
  cellDataApiUrl: '',
  scannerIp: '',
  scannerPort: 0,
  barcodeMatchRegex: '',
  technicsProcessCode: 'CTP_M1130',
  technicsProcessName: '堆叠站',
  produce_Type: '2',
  tenantID: 'FD',
  plcIp: '192.168.2.1',
  plcCpu: 'S71500',
  plcRack: 0,
  plcSlot: 1,
  plcHeartbeatAddress: 'DB1590.DBX215.0',
  plcAStackDbNum: 1590,
  plcBStackDbNum: 1591,
  plcAStackFinishAddress: 'DB1590.DBX0.0',
  plcBStackFinishAddress: 'DB1591.DBX0.0',
  plcCellLayerAddress: 'DB1.DBB1',
  cellsPerLayer: 12,
  col1StartAddr: 'DB1.DBB10',
  col2StartAddr: 'DB1.DBB11',
  col3StartAddr: 'DB1.DBB12',
  cellBarcodeLength: 24,
  adminUsername: 'admin',
  adminPassword: '123'
};
