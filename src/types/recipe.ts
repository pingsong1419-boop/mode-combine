export interface RecipeMaster {
  id: string;
  productName: string;
  cellCount: string;
  plcModel: string;
  createTime: string;
  isActive?: boolean;
}

export interface RecipeDetail {
  id: string;
  masterId: string;
  productName: string;
  moduleType: string;
  productSpec: string;
  blockIndex: string;
  blockSpec: string;
  orderType: string; // 0, 1, 2
  createTime: string;
}
