#include <algorithm>
#include <climits>
#include <cmath>
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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    // Lets divide all the rooms on block by n elements. Every guest will move
    // to a new room R, lets find the R mod n. We are fine if all of those
    // reminders are differ. O(n) solution.

    set<int> hotel;
    bool res = true;
    for (int idx = 0; idx < static_cast<int>(n); ++idx) {
      auto next_room_rem = (idx + a[idx]) % static_cast<int>(n);
      while (next_room_rem < 0) {
        next_room_rem += n;
      }
      if (hotel.find(next_room_rem) != hotel.end()) {
        res = false;
        break;
      }
      hotel.insert(next_room_rem);
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
