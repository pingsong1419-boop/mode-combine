// MES系统接口类型定义

/** 应用配置 */
export interface AppConfig {
  /** 非首段工序API地址 */
  orderApiUrl: string
  /** 工步工序API地址 */
  routeApiUrl: string
  /** 单件物料验证API */
  singleMaterialApiUrl?: string
  /** 全量物料验证API */
  fullMaterialApiUrl?: string
  /** 模组码生成API */
  codeCreateApiUrl?: string
  /** MES 信息推送API */
  mesPushApiUrl?: string
  /** 重码校验API */
  duplicateCheckApiUrl?: string
  /** 获取电芯数据API */
  cellDataApiUrl?: string
  
  /** 扫码枪 IP */
  scannerIp?: string
  /** 扫码枪端口 */
  scannerPort?: number
  /** 条码匹配正则 */
  barcodeMatchRegex?: string
  
  /** 工序代码 */
  technicsProcessCode: string
  /** 生产类型 */
  produce_Type?: string
  /** 租户 ID */
  tenantID?: string
  
  /** PLC 配置 */
  plcIp?: string
  plcCpu?: string
  plcRack?: number
  plcSlot?: number
  plcHeartbeatAddress?: string
  /** A面堆叠完成信号点位 */
  plcAStackFinishAddress?: string
  /** B面堆叠完成信号点位 */
  plcBStackFinishAddress?: string
  /** 电芯层数读取点位 (BYTE) */
  plcCellLayerAddress?: string
  /** 每层电芯个数 (整型) */
  cellsPerLayer?: number
  
  /** 1列电芯码起始位置 (BYTE) */
  col1StartAddr?: string
  /** 2列电芯码起始位置 (BYTE) */
  col2StartAddr?: string
  /** 3列电芯码起始位置 (BYTE) */
  col3StartAddr?: string
  /** 电芯码长度 (整型) */
  cellBarcodeLength?: number
  
  /** 管理员设置 */
  adminUsername?: string
  adminPassword?: string
}

/** 获取工单请求参数 */
export interface GetOrderRequest {
  /** 生产类型 */
  produce_Type?: string
  /** 租户 ID */
  tenantID?: string
}

/**
 * 工单信息（接口一 datas[] 中的单条数据）
 * route_No：工艺路线编码，传给接口二的 routeCode
 */
export interface OrderInfo {
  orderCode: string
  /** 备用工单号字段 */
  code?: string
  route_No: string       // 注意：实际字段名带下划线
  specsCode?: string
  cell_Level?: string | null
  cell_Batch?: string | null
  productMixCode?: string | null
  projectCode?: string
  productline_no?: string
  polarity?: number
  relateOrderNo?: string
  moduleSort?: string
  orderType?: number
  /** 工单状态 (例如：2 表示已发布/待生产) */
  order_Status?: number | string
  [key: string]: unknown
}

/**
 * 获取工单响应
 * 实际结构：{ code, message, datas: OrderInfo[] }
 */
export interface GetOrderResponse {
  code: number | string
  message?: string
  msg?: string
  /** 实际返回的是 datas 数组 */
  datas?: OrderInfo[]
  /** 兼容 data 字段 */
  data?: OrderInfo | OrderInfo[] | null
  success?: boolean
}

/** 获取工步列表请求 */
export interface GetRouteRequest {
  routeCode: string   // 来自接口一的 route_No
  workSeqNo: string   // 工序代码
}

/** 工步信息（接口二 workSeqList[] 中的数据，字段全小写） */
export interface RouteStep {
  workseqNo?: string      // 工步编码
  workseqName?: string    // 工步名称
  sortCode?: number       // 排序
  routeId?: string
  workseqId?: string
  workSeqParamList?: unknown[]
  workSeqMaterialList?: unknown[]
  workStepList?: WorkStep[]
  [key: string]: unknown
}

/** 工步参数 (workStepParamList 中的单条) */
export interface WorkStepParam {
  paramName?: string          // 参数名称
  minQualityValue?: string | number | null
  maxQualityValue?: string | number | null
  standardValue?: string | number | null
  paramUnit?: string
  [key: string]: unknown
}

/** 工步下的子步骤 (workStepList 中的单条)
 *  对应 LabVIEW DataOut 结构 */
export interface WorkStep {
  workstepNo?: string
  workstepName?: string       // 对应 DataOut.workstepName
  pSetNo?: string | number | null  // 对应 DataOut.pSetNo
  torqueSettingCount?: number | null  // 对应 DataOut.torqueSettingCount
  workstepType?: number       // 对应 DataOut.workstepType
  docUrl?: string | null      // 对应 DataOut.docUrl
  sortCode?: number
  remark?: string | null
  workStepParamList?: WorkStepParam[]   // 对应 DataOut.Paramlist_out
  workStepMaterialList?: unknown[]
  workStepDocList?: unknown[]
  workStepLineList?: unknown[]
  [key: string]: unknown
}

/** 工步列表数据块（接口二 data 内容） */
export interface RouteData {
  workSeqList?: RouteStep[]
  [key: string]: unknown
}

/**
 * 获取工步列表响应
 * 实际结构：{ code, message, data: { workSeqList: RouteStep[] } }
 */
export interface GetRouteResponse {
  code: number | string
  message?: string
  msg?: string
  data?: RouteData | RouteStep[] | null
  success?: boolean
}

/** 测试结果状态 */
export type TestResult = 'IDLE' | 'OK' | 'NG'

/** LabVIEW通信信号内容 */
export interface LabviewSignal {
  result: TestResult
  orderCode: string
  productCode: string
  timestamp: string
  routeNo: string
}
