#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, m;
    std::cin >> n >> m;

    vector<int> friends(n);
    for (auto& i : friends) {
      std::cin >> i;
    }

    vector<int> costs(m);
    for (auto& i : costs) {
      std::cin >> i;
    }

    sort(friends.begin(), friends.end());

    // We start buying all presence which costs less, than required
    // cost for friend.
    int cheapest_idx = 0;
    int64_t res = 0;
    while (!friends.empty()) {
      if (costs[friends.back() - 1] > costs[cheapest_idx]) {
        // Byu cheapest gift for a "largest" friend.
        res += costs[cheapest_idx++];
      } else {
        // Pay C_{k_i} directly.
        res += costs[friends.back() - 1];
      }

      friends.pop_back();
    }

    std::cout << res << "\n";
  }

  return 0;
}
