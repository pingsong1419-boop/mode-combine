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
  scannerIp: '',
  scannerPort: 0,
  barcodeMatchRegex: '',
  technicsProcessCode: 'CTP_M1130',
  produce_Type: '2',
  tenantID: 'FD',
  plcIp: '192.168.10.9',
  plcCpu: 'S71200',
  plcRack: 0,
  plcSlot: 1,
  plcHeartbeatAddress: 'DB1.DBX0.0',
  plcAStackFinishAddress: 'DB1.DBX0.1',
  plcBStackFinishAddress: 'DB1.DBX0.2',
  plcCellLayerAddress: 'DB1.DBB1',
  cellsPerLayer: 12,
  col1StartAddr: 'DB1.DBB10',
  col2StartAddr: 'DB1.DBB11',
  col3StartAddr: 'DB1.DBB12',
  cellBarcodeLength: 24,
  adminUsername: 'admin',
  adminPassword: '123'
};
