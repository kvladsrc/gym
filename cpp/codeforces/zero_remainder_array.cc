#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    std::map<int, int> rems;
    int max_bucket = 0;
    rems[max_bucket] = 0;

    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;

      int rem = buf % k, delta = 0;
      if (rem == 0) {
        continue;
      } else {
        delta = k - rem;
      }

      rems[delta]++;

      if (rems[delta] > rems[max_bucket]) {
        max_bucket = delta;
        continue;
      }

      if (rems[delta] == rems[max_bucket]) {
        max_bucket = max(max_bucket, delta);
      }
    }

    // Finding largest number of moves to solve the reminder
    // bucket. We solve rest of the elements meanwhile.
    if (max_bucket == 0) {
      std::cout << 0 << "\n";
      continue;
    }

    int64_t need_moves = max_bucket;
    need_moves +=
        static_cast<int64_t>((rems[max_bucket] - 1)) * static_cast<int64_t>(k);
    // Apply to the last value. All values less
    // applied already. We just use first type of
    // operations instead of incrementing.
    need_moves += 1;

    std::cout << need_moves << "\n";
  }

  return 0;
}
