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
using std::gcd;
using std::map;
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
    int n;
    int64_t k;
    std::cin >> n >> k;

    int64_t attacks = k / 2;
    if (k % 2) {
      attacks++;
    }

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    bool reversed = false;
    // Use push, pop. Starts by front.
    std::reverse(a.begin(), a.end());
    while (attacks && !a.empty()) {
      auto last = a.back();
      a.pop_back();
      int damage = min(attacks, static_cast<int64_t>(last));
      last -= damage;
      attacks -= damage;

      if (attacks == 0) {
        if (last) {
          a.push_back(last);
        }

        if (!reversed) {
          // Back attacks.
          reversed = true;
          std::reverse(a.begin(), a.end());
          attacks = k / 2;
        }
      }
    }

    std::cout << n - a.size() << "\n";
  }

  return 0;
}
