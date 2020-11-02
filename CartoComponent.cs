using LiveSplit.Model;
using LiveSplit.VoxSplitter;

namespace LiveSplit.Carto {
    public class CartoComponent : Component {

        protected override EGameTime GameTime => EGameTime.Loading;

        public CartoComponent(LiveSplitState state) : base(state) {
            memory = new CartoMemory(logger);
            settings = new TreeSettings(state, Start, Reset, Options);
        }
    }
}