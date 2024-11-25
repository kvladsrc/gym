#include <algorithm>
#include <climits>
#include <cmath>
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

bool compare_pair(const pair<int, int> &a, const pair<int, int> &b) {
  if (a.first == b.first) {
    return a.second < b.second;
  }

  return a.first < b.first;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<pair<int, int>> packages(n);
    for (auto &p : packages) {
      std::cin >> p.first >> p.second;
    }

    sort(packages.begin(), packages.end(), compare_pair);

    int cur_x = 0, cur_y = 0;
    string res;
    for (auto p : packages) {
      while (p.first > cur_x) {
        cur_x++;
        res += 'R';
      }

      if (p.second < cur_y) {
        res.clear();
        break;
      }

      while (cur_y < p.second) {
        cur_y++;
        res += 'U';
      }
    }

    if (res.size() > 0) {
      std::cout << "YES"
                << "\n";
      std::cout << res << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
