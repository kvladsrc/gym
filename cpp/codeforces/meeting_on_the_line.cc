#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iomanip>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

using meta = pair<long double, long double>;

meta meet(meta a, meta b) {
  if (a.second > b.second) {
    std::swap(a, b);
  }

  auto a_flat = a.second - a.first;
  auto b_flat = b.second + b.first;
  auto meet_point = (a_flat + b_flat) / 2.0;
  meet_point = max(meet_point, a.second);
  meet_point = min(meet_point, b.second);

  auto time_to_meet = abs(a_flat - b_flat) / 2.0;
  time_to_meet = max(time_to_meet, a.first);
  time_to_meet = max(time_to_meet, b.first);

  return {time_to_meet, meet_point};
}

int main(int /*argc*/, char* /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;

    vector<meta> pos_and_time(n);
    for (auto& i : pos_and_time) {
      std::cin >> i.second;
    }

    for (auto& i : pos_and_time) {
      std::cin >> i.first;
    }

    meta T = {-INT_MAX, 0};
    auto max_it = std::max_element(
        pos_and_time.begin(), pos_and_time.end(),
        [](const meta& a, const meta& b) { return a.first < b.first; });
    if (max_it != pos_and_time.end()) {
      T = *max_it;
    }

    // EDGE_CASE: All elements can reach position of "longest to
    // wait" element before T get ready.
    long double edge_case_time = 0;
    for (auto p : pos_and_time) {
      if (p == T) {
        continue;
      }

      edge_case_time = max(edge_case_time, p.first + abs(p.second - T.second));
    }

    if (edge_case_time < T.first) {
      std::cout << std::fixed << std::setprecision(7) << T.second << "\n";
      continue;
    }

    /*
     * We need to find most left element L and average distance
     * between L and element took maximal amount of time to
     * reach. Prevous block exclude edge case, so all elements shall
     * move. So minimal amount of time is at least T.second. Lets
     * assume, that all elements are moving to the right position
     * (even if do not know where it will be). While whole process at
     * least two elements shall move to each other, otherwise solution
     * is suboptimal, so we need to find two elements which will meet
     * last. Guess, one of them will be top left element.
     */

    meta L = {0, 0};
    long double can_be_global = INT_MAX;
    for (auto p : pos_and_time) {
      auto can_be_local = p.second - p.first + T.first;

      if (can_be_local < can_be_global) {
        can_be_global = can_be_local;
        L = p;
      }
    }

    auto cur = L;

    for (auto p : pos_and_time) {
      if (p == L) {
        continue;
      }

      auto m = meet(L, p);
      if (m.first > cur.first ||
          (m.first == cur.first && m.second > cur.second)) {
        cur = m;
        continue;
      }
    }

    std::cout << std::fixed << std::setprecision(7) << cur.second << "\n";
  }

  return 0;
}
