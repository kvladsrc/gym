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

class CompareRanges {
 public:
  bool operator()(const pair<int, int> &a, const pair<int, int> &b) const {
    if ((a.second - a.first) == (b.second - b.first)) {
      return a.first > b.first;
    }

    return (a.second - a.first) < (b.second - b.first);
  }
};

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    priority_queue<pair<int, int>, vector<pair<int, int>>, CompareRanges> q;
    vector<int> res(n);
    q.push({0, n - 1});

    int action = 1;
    while (!q.empty()) {
      auto largest = q.top();
      q.pop();

      auto pos = (largest.first + largest.second) / 2;
      res[pos] = action++;

      if (largest.second == largest.first) {
        continue;
      }

      if (largest.second - largest.first == 1) {
        q.push({pos + 1, largest.second});
        continue;
      }

      if ((largest.second - largest.first + 1) % 2) {
        q.push({largest.first, pos - 1});
        q.push({pos + 1, largest.second});
      } else {
        q.push({pos + 1, largest.second});
        q.push({largest.first, pos - 1});
      }
    }

    for (auto i : res) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
