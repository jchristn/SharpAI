import { LightGraphTheme, primaryTheme, darkTheme } from '#/theme/theme';
import { ThemeConfig } from 'antd';

describe('Theme Configuration', () => {
  describe('LightGraphTheme', () => {
    it('should have all required color properties', () => {
      expect(LightGraphTheme).toHaveProperty('primary');
      expect(LightGraphTheme).toHaveProperty('primaryLight');
      expect(LightGraphTheme).toHaveProperty('primaryExtraLight');
      expect(LightGraphTheme).toHaveProperty('primaryRed');
      expect(LightGraphTheme).toHaveProperty('secondaryBlue');
      expect(LightGraphTheme).toHaveProperty('secondaryYellow');
      expect(LightGraphTheme).toHaveProperty('borderGray');
      expect(LightGraphTheme).toHaveProperty('borderGrayDark');
      expect(LightGraphTheme).toHaveProperty('white');
    });

    it('should have correct color values', () => {
      expect(LightGraphTheme.primary).toBe('#CB3B84');
      expect(LightGraphTheme.primaryLight).toBe('#fa43a0');
      expect(LightGraphTheme.primaryExtraLight).toBe('#F6EBF6');
      expect(LightGraphTheme.primaryRed).toBe('#B2B0E8');
      expect(LightGraphTheme.secondaryBlue).toBe('#b1e5ff');
      expect(LightGraphTheme.secondaryYellow).toBe('#ffe362');
      expect(LightGraphTheme.borderGray).toBe('#C1C1C1');
      expect(LightGraphTheme.borderGrayDark).toBe('#666666');
      expect(LightGraphTheme.white).toBe('#ffffff');
    });

    it('should have font family configuration', () => {
      expect(LightGraphTheme).toHaveProperty('fontFamily');
      expect(LightGraphTheme.fontFamily).toBe('"Inter", "serif"');
    });

    it('should have disabled state colors', () => {
      expect(LightGraphTheme).toHaveProperty('colorBgContainerDisabled');
      expect(LightGraphTheme).toHaveProperty('colorBgContainerDisabledDark');
      expect(LightGraphTheme).toHaveProperty('textDisabled');

      expect(LightGraphTheme.colorBgContainerDisabled).toBe('#E9E9E9');
      expect(LightGraphTheme.colorBgContainerDisabledDark).toBe('#555555');
      expect(LightGraphTheme.textDisabled).toBe('#bbbbbb');
    });

    it('should have sub heading color', () => {
      expect(LightGraphTheme).toHaveProperty('subHeadingColor');
      expect(LightGraphTheme.subHeadingColor).toBe('#666666');
    });
  });

  describe('primaryTheme', () => {
    it('should be a valid ThemeConfig', () => {
      expect(primaryTheme).toBeDefined();
      expect(typeof primaryTheme).toBe('object');
    });

    it('should have cssVar enabled', () => {
      expect(primaryTheme.cssVar).toBe(true);
    });

    it('should have default algorithm', () => {
      expect(primaryTheme.algorithm).toBeDefined();
    });

    it('should have token configuration', () => {
      expect(primaryTheme.token).toBeDefined();
      expect(primaryTheme.token.colorBgElevated).toBe('#f5f5f5');
      expect(primaryTheme.token.colorBgBase).toBe('#ffffff');
      expect(primaryTheme.token.colorPrimary).toBe(LightGraphTheme.primary);
      expect(primaryTheme.token.fontFamily).toBe(LightGraphTheme.fontFamily);
      expect(primaryTheme.token.colorBorder).toBe(LightGraphTheme.borderGray);
      expect(primaryTheme.token.colorTextDisabled).toBe(LightGraphTheme.textDisabled);
      expect(primaryTheme.token.colorBgContainerDisabled).toBe(LightGraphTheme.colorBgContainerDisabled);
    });

    it('should have components configuration', () => {
      expect(primaryTheme.components).toBeDefined();
      expect(primaryTheme.components.Tabs).toBeDefined();
      expect(primaryTheme.components.Typography).toBeDefined();
      expect(primaryTheme.components.Layout).toBeDefined();
      expect(primaryTheme.components.Menu).toBeDefined();
      expect(primaryTheme.components.Button).toBeDefined();
      expect(primaryTheme.components.Table).toBeDefined();
      expect(primaryTheme.components.Collapse).toBeDefined();
      expect(primaryTheme.components.Input).toBeDefined();
      expect(primaryTheme.components.Select).toBeDefined();
      expect(primaryTheme.components.Pagination).toBeDefined();
      expect(primaryTheme.components.Form).toBeDefined();
    });

    it('should have correct Tabs configuration', () => {
      const tabsConfig = primaryTheme.components.Tabs;
      expect(tabsConfig.cardBg).toBe('#F2F2F2');
      expect(tabsConfig.titleFontSize).toBe(12);
    });

    it('should have correct Typography configuration', () => {
      const typographyConfig = primaryTheme.components.Typography;
      expect(typographyConfig.fontWeightStrong).toBe(400);
    });

    it('should have correct Layout configuration', () => {
      const layoutConfig = primaryTheme.components.Layout;
      expect(layoutConfig.fontFamily).toBe(LightGraphTheme.fontFamily);
    });

    it('should have correct Menu configuration', () => {
      const menuConfig = primaryTheme.components.Menu;
      expect(menuConfig.itemSelectedBg).toBe(LightGraphTheme.primaryExtraLight);
    });

    it('should have correct Button configuration', () => {
      const buttonConfig = primaryTheme.components.Button;
      expect(buttonConfig.borderRadius).toBe(4);
      expect(buttonConfig.primaryColor).toBe(LightGraphTheme.white);
      expect(buttonConfig.defaultColor).toBe('#333333');
      expect(buttonConfig.colorLink).toBe(LightGraphTheme.primary);
      expect(buttonConfig.colorLinkHover).toBe(LightGraphTheme.primary);
    });

    it('should have correct Table configuration', () => {
      const tableConfig = primaryTheme.components.Table;
      expect(tableConfig.headerBg).toBe('#ffffff');
      expect(tableConfig.padding).toBe(18);
      expect(tableConfig.borderColor).toBe('#d1d5db');
    });

    it('should have correct Collapse configuration', () => {
      const collapseConfig = primaryTheme.components.Collapse;
      expect(collapseConfig.headerBg).toBe(LightGraphTheme.white);
    });

    it('should have correct Input configuration', () => {
      const inputConfig = primaryTheme.components.Input;
      expect(inputConfig.borderRadiusLG).toBe(3);
      expect(inputConfig.borderRadius).toBe(3);
      expect(inputConfig.borderRadiusXS).toBe(3);
    });

    it('should have correct Select configuration', () => {
      const selectConfig = primaryTheme.components.Select;
      expect(selectConfig.borderRadiusLG).toBe(3);
      expect(selectConfig.borderRadius).toBe(3);
      expect(selectConfig.borderRadiusXS).toBe(3);
      expect(selectConfig.optionSelectedColor).toBe(LightGraphTheme.white);
      expect(selectConfig.optionSelectedBg).toBe(LightGraphTheme.primary);
    });

    it('should have correct Pagination configuration', () => {
      const paginationConfig = primaryTheme.components.Pagination;
      expect(paginationConfig.fontFamily).toBe(LightGraphTheme.fontFamily);
    });

    it('should have correct Form configuration', () => {
      const formConfig = primaryTheme.components.Form;
      expect(formConfig.labelColor).toBe(LightGraphTheme.subHeadingColor);
      expect(formConfig.colorBorder).toBe('none');
      expect(formConfig.verticalLabelPadding).toBe(0);
      expect(formConfig.itemMarginBottom).toBe(10);
    });
  });

  describe('darkTheme', () => {
    it('should be a valid ThemeConfig', () => {
      expect(darkTheme).toBeDefined();
      expect(typeof darkTheme).toBe('object');
    });

    it('should have cssVar enabled', () => {
      expect(darkTheme.cssVar).toBe(true);
    });

    it('should have dark algorithm', () => {
      expect(darkTheme.algorithm).toBeDefined();
    });

    it('should have token configuration for dark mode', () => {
      expect(darkTheme.token).toBeDefined();
      expect(darkTheme.token.colorBgElevated).toBe('#353535');
      expect(darkTheme.token.colorBgBase).toBe('#222222');
      expect(darkTheme.token.colorPrimary).toBe(LightGraphTheme.primaryLight);
      expect(darkTheme.token.fontFamily).toBe(LightGraphTheme.fontFamily);
      expect(darkTheme.token.colorBorder).toBe(LightGraphTheme.borderGrayDark);
      expect(darkTheme.token.colorTextDisabled).toBe(LightGraphTheme.textDisabled);
      expect(darkTheme.token.colorBgContainerDisabled).toBe(LightGraphTheme.colorBgContainerDisabledDark);
    });

    it('should have components configuration for dark mode', () => {
      expect(darkTheme.components).toBeDefined();
      expect(darkTheme.components.Tabs).toBeDefined();
      expect(darkTheme.components.Typography).toBeDefined();
      expect(darkTheme.components.Layout).toBeDefined();
      expect(darkTheme.components.Menu).toBeDefined();
      expect(darkTheme.components.Button).toBeDefined();
      expect(darkTheme.components.Table).toBeDefined();
      expect(darkTheme.components.Collapse).toBeDefined();
      expect(darkTheme.components.Input).toBeDefined();
      expect(darkTheme.components.Select).toBeDefined();
      expect(darkTheme.components.Pagination).toBeDefined();
      expect(darkTheme.components.Form).toBeDefined();
    });

    it('should have correct Menu configuration for dark mode', () => {
      const menuConfig = darkTheme.components.Menu;
      expect(menuConfig.itemSelectedBg).toBe('#222222');
      expect(menuConfig.itemSelectedColor).toBe(LightGraphTheme.primaryLight);
    });

    it('should have correct Button configuration for dark mode', () => {
      const buttonConfig = darkTheme.components.Button;
      expect(buttonConfig.borderRadius).toBe(4);
      expect(buttonConfig.primaryColor).toBe(LightGraphTheme.white);
      expect(buttonConfig.defaultColor).toBe('var(--ant-color-text-base)');
      expect(buttonConfig.colorLink).toBe(LightGraphTheme.primaryLight);
      expect(buttonConfig.colorLinkHover).toBe(LightGraphTheme.primaryLight);
    });

    it('should have correct Table configuration for dark mode', () => {
      const tableConfig = darkTheme.components.Table;
      expect(tableConfig.headerBg).toBe('var(--ant-color-bg-container)');
      expect(tableConfig.padding).toBe(18);
      expect(tableConfig.borderColor).toBe('var(--ant-color-border)');
    });

    it('should have correct Form configuration for dark mode', () => {
      const formConfig = darkTheme.components.Form;
      expect(formConfig.labelColor).toBe('var(--ant-color-text-base)');
      expect(formConfig.colorBorder).toBe('none');
      expect(formConfig.verticalLabelPadding).toBe(0);
      expect(formConfig.itemMarginBottom).toBe(10);
    });

    it('should use CSS variables for dynamic theming', () => {
      const buttonConfig = darkTheme.components.Button;
      const tableConfig = darkTheme.components.Table;
      const formConfig = darkTheme.components.Form;

      expect(buttonConfig.defaultColor).toContain('var(--ant-color-');
      expect(tableConfig.headerBg).toContain('var(--ant-color-');
      expect(tableConfig.borderColor).toContain('var(--ant-color-');
      expect(formConfig.labelColor).toContain('var(--ant-color-');
    });
  });

  describe('Theme Consistency', () => {
    it('should have consistent font family across themes', () => {
      expect(primaryTheme.token.fontFamily).toBe(darkTheme.token.fontFamily);
      expect(primaryTheme.components.Layout.fontFamily).toBe(darkTheme.components.Layout.fontFamily);
      expect(primaryTheme.components.Pagination.fontFamily).toBe(darkTheme.components.Pagination.fontFamily);
    });

    it('should have consistent border radius values', () => {
      expect(primaryTheme.components.Button.borderRadius).toBe(darkTheme.components.Button.borderRadius);
      expect(primaryTheme.components.Input.borderRadius).toBe(darkTheme.components.Input.borderRadius);
      expect(primaryTheme.components.Input.borderRadiusLG).toBe(darkTheme.components.Input.borderRadiusLG);
      expect(primaryTheme.components.Input.borderRadiusXS).toBe(darkTheme.components.Input.borderRadiusXS);
      expect(primaryTheme.components.Select.borderRadius).toBe(darkTheme.components.Select.borderRadius);
      expect(primaryTheme.components.Select.borderRadiusLG).toBe(darkTheme.components.Select.borderRadiusLG);
      expect(primaryTheme.components.Select.borderRadiusXS).toBe(darkTheme.components.Select.borderRadiusXS);
    });

    it('should have consistent padding and spacing', () => {
      expect(primaryTheme.components.Table.padding).toBe(darkTheme.components.Table.padding);
      expect(primaryTheme.components.Form.verticalLabelPadding).toBe(darkTheme.components.Form.verticalLabelPadding);
      expect(primaryTheme.components.Form.itemMarginBottom).toBe(darkTheme.components.Form.itemMarginBottom);
    });

    it('should have consistent typography settings', () => {
      expect(primaryTheme.components.Typography.fontWeightStrong).toBe(
        darkTheme.components.Typography.fontWeightStrong
      );
      expect(primaryTheme.components.Tabs.titleFontSize).toBe(darkTheme.components.Tabs.titleFontSize);
    });

    it('should have different color schemes for light and dark modes', () => {
      expect(primaryTheme.token.colorBgBase).not.toBe(darkTheme.token.colorBgBase);
      expect(primaryTheme.token.colorBgElevated).not.toBe(darkTheme.token.colorBgElevated);
      expect(primaryTheme.token.colorPrimary).not.toBe(darkTheme.token.colorPrimary);
      expect(primaryTheme.token.colorBorder).not.toBe(darkTheme.token.colorBorder);
    });
  });

  describe('Theme Validation', () => {
    it('should have valid hex color values', () => {
      const hexColorRegex = /^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$/;

      // Test LightGraphTheme colors
      Object.values(LightGraphTheme).forEach((value) => {
        if (typeof value === 'string' && value.startsWith('#')) {
          expect(value).toMatch(hexColorRegex);
        }
      });

      // Test primaryTheme token colors
      Object.values(primaryTheme.token).forEach((value) => {
        if (typeof value === 'string' && value.startsWith('#')) {
          expect(value).toMatch(hexColorRegex);
        }
      });

      // Test darkTheme token colors
      Object.values(darkTheme.token).forEach((value) => {
        if (typeof value === 'string' && value.startsWith('#')) {
          expect(value).toMatch(hexColorRegex);
        }
      });
    });

    it('should have valid CSS variable references', () => {
      const cssVarRegex = /^var\(--ant-color-[a-zA-Z-]+\)$/;

      // Test darkTheme CSS variables
      const darkThemeValues = [
        darkTheme.components.Button.defaultColor,
        darkTheme.components.Table.headerBg,
        darkTheme.components.Table.borderColor,
        darkTheme.components.Form.labelColor,
      ];

      darkThemeValues.forEach((value) => {
        if (typeof value === 'string' && value.startsWith('var(')) {
          expect(value).toMatch(cssVarRegex);
        }
      });
    });

    it('should have consistent component structure', () => {
      const primaryComponents = Object.keys(primaryTheme.components);
      const darkComponents = Object.keys(darkTheme.components);

      expect(primaryComponents.sort()).toEqual(darkComponents.sort());
    });
  });
});
