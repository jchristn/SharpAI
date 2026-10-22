import {useTranslation} from 'react-i18next';
import {CopyButton,Modal} from '../../components/ui';
import type {LibraryItem} from '../../api/library';
import {generationFields} from '../../api/library';
/** Read-only inspection never starts a save workflow or changes the selected scope. */
export function LibraryPreview({item,onClose,onDuplicate}:{item:LibraryItem;onClose:()=>void;onDuplicate:()=>void}):JSX.Element {
 const {t,i18n}=useTranslation();const config=item.Configuration;
 return <Modal wide open title={item.Name} onClose={onClose} footer={<><button className="sa-btn" onClick={onClose}>{t('common.close')}</button><button className="sa-btn sa-btn--primary" title={t('library.duplicateHelp')} onClick={onDuplicate}>{t('library.duplicate')}</button></>}>
  <p className="sa-page-description">{t(`library.${item.Scope}`)} · {item.Description}</p>
  {item.Content!=null?<><header className="sa-payload-heading"><h3>{t('chat.system')}</h3><CopyButton value={item.Content}/></header><pre className="sa-payload-text">{item.Content}</pre></>:config?<><header className="sa-payload-heading"><h3>{t('library.configurationSelector')}</h3><CopyButton value={JSON.stringify(config,null,2)}/></header><dl className="sa-detail-metadata">{generationFields.map(({field:key,key:label})=><div key={key}><dt title={t(`samplingHelp.${label}`)}>{t(`sampling.${label}`)}</dt><dd>{config[key]==null?t('chat.serverDefault'):new Intl.NumberFormat(i18n.language).format(config[key]!)}</dd></div>)}
   <div><dt>{t('chat.stop')}</dt><dd>{config.Stop.join(', ')||'—'}</dd></div>{(['StreamResponse','JsonOutput','DisplayThinking'] satisfies Array<'StreamResponse'|'JsonOutput'|'DisplayThinking'>).map((key,index)=><div key={key}><dt>{t(['chat.streamToggle','chat.json','chat.thinking'][index])}</dt><dd>{t(config[key]?'admin.yes':'admin.no')}</dd></div>)}
  </dl></>:null}
 </Modal>;
}
