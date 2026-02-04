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
  int n;
  std::cin >> n;

  vector<int> cal(7);
  int pages_per_week = 0;
  for (int i = 0; i < 7; ++i) {
    std::cin >> cal[i];
    pages_per_week += cal[i];
  }

  n %= pages_per_week;
  int day;
  if (!n) {
    auto it = std::find_if(cal.rbegin(), cal.rend(),
                           [](int pages) { return pages > 0; });
    if (it != cal.rend()) {
      day = 6 - std::distance(cal.rbegin(), it);
    }
  } else {
    day = 6;
  }

  while (n > 0) {
    day = (day + 1) % 7;
    n -= cal[day];
  }

  std::cout << day + 1 << std::endl;

  return 0;
}
