#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

bool compare(const pair<int, int> &a, const pair<int, int> &b) {
  return a.first < b.first;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    vector<pair<int, int>> ab(n);
    for (auto &p : ab) {
      std::cin >> p.first;
    }
    for (auto &p : ab) {
      std::cin >> p.second;
    }

    sort(ab.begin(), ab.end(), compare);
    for (const auto &p : ab) {
      if (k >= p.first) {
        k += p.second;
      } else {
        break;
      }
    }

    std::cout << k << "\n";
  }

  return 0;
}
