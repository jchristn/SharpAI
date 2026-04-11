import React, { useEffect, useState } from 'react';
import { Table, TableProps } from 'antd';
import { Resizable } from 'react-resizable';

const ResizableTitle = (props: any) => {
  const { onResize, width, ...restProps } = props;

  if (!width) {
    return <th {...restProps} />;
  }

  return (
    <Resizable
      width={width}
      height={0}
      handle={
        <span
          className="react-resizable-handle"
          onClick={(e) => {
            e.stopPropagation();
          }}
        />
      }
      onResize={onResize}
      draggableOpts={{ enableUserSelectHack: false }}
    >
      <th {...restProps} />
    </Resizable>
  );
};

const SharpTable = (props: TableProps) => {
  const { columns, pagination, ...rest } = props;

  // Default pagination position to topRight
  const paginationConfig =
    pagination === false
      ? false
      : pagination
      ? { position: ["topRight" as const], ...pagination }
      : { position: ["topRight" as const] };
  const [columnsState, setColumnsState] = useState(columns);

  const handleResize =
    (index: number) =>
    (e: any, { size }: any) => {
      setColumnsState((prev: any) => {
        const nextColumns = [...prev];
        nextColumns[index] = {
          ...nextColumns[index],
          width: size.width,
        };
        return nextColumns;
      });
    };

  const columnsWithResizable = columnsState?.map((col: any, index: number) => ({
    ...col,
    onHeaderCell: (column: any) => ({
      width: column.width,
      onResize: handleResize(index),
    }),
  }));

  useEffect(() => {
    setColumnsState(columns);
  }, [columns]);

  return (
    <Table
      {...rest}
      pagination={paginationConfig as TableProps["pagination"]}
      columns={columnsWithResizable}
      components={{
        header: {
          cell: ResizableTitle,
        },
      }}
    />
  );
};

export default SharpTable;
