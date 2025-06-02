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
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    map<int, int> m;
    int pos_cnt = 0, neg_cnt = 0;
    for (auto &i : a) {
      std::cin >> i;
      if (i > 0) pos_cnt++;
      if (i < 0) neg_cnt++;
      m[i]++;
    }

    if (pos_cnt > 2 || neg_cnt > 2) {
      std::cout << "NO" << "\n";
      continue;
    }

    // If we have 3 different positive numbers than the answer is NO,
    // same for a negatives. Proof: a + b + c = d, d is bigger than a,
    // b, c. a + b + d = e, e > d, a + b + e = f, f > e and so on. So
    // we have at most 2 positive, 2 negatives and zeroes. Lets check
    // all the combinations.

    vector<int> brute_force;
    for (auto p : m) {
      int to_add = min(3, p.second);  // Many zeroes. No need more than
                                      // 3 nums because of a + b + c.
      while (to_add--) {
        brute_force.push_back(p.first);
      }
    }

    bool res = true;
    for (size_t first = 0; first < brute_force.size(); ++first) {
      for (size_t second = first + 1; second < brute_force.size(); ++second) {
        for (size_t third = second + 1; third < brute_force.size(); ++third) {
          auto sum =
              brute_force[first] + brute_force[second] + brute_force[third];
          if (m[sum] == 0) {
            res = false;
            break;
          }
        }
        if (!res) {
          break;
        }
      }
      if (!res) {
        break;
      }
    }

    if (res) {
      std::cout << "YES" << "\n";
    } else {
      std::cout << "NO" << "\n";
    }
  }

  return 0;
}
