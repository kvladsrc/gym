#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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

int main(int argc, char* argv[]) {
  int d, sum_time;
  std::cin >> d >> sum_time;
  vector<pair<int, int>> per_day(d);
  for (auto& p : per_day) {
    std::cin >> p.first >> p.second;
  }

  vector<int> res(d);
  bool ok = true;

  // Filling by mins.
  for (int i = 0; i < d; ++i) {
    res[i] = per_day[i].first;
    sum_time -= per_day[i].first;
    if (sum_time < 0) {
      ok = false;
      break;
    }
  }

  // Greedy use of the extra time.
  for (int pos = 0; pos < d; ++pos) {
    int delta = min(per_day[pos].second - per_day[pos].first, sum_time);
    res[pos] += delta;
    sum_time -= delta;
  }

  if (sum_time) ok = false;

  if (ok) {
    std::cout << "YES"
              << "\n";
    for (auto i : res) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
