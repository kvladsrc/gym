#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

#define MONTH 12

int main(int argc, char* argv[]) {
  int k;
  std::cin >> k;
  vector<int> month(MONTH);
  for (int i = 0; i < MONTH; ++i) {
    std::cin >> month[i];
  }

  // Need to reverse sort the month
  sort(month.begin(), month.end(), std::greater<int>());

  int growth = 0;
  int res = 0;
  while (growth < k) {
    if (res == MONTH) {
      res = -1;
      break;
    }

    growth += month[res++];
  }

  std::cout << res << "\n";

  return 0;
}
