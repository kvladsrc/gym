#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n, t;
  std::cin >> n >> t;

  // 2 <= t <= 10
  int t_digits;
  if (t == 10) {
    t_digits = 2;
  } else {
    t_digits = 1;
  }

  string res;
  res += std::to_string(t);
  while (t_digits < n) {
    res += '0';
    t_digits++;
  }

  if (t_digits == n) {
    std::cout << res << "\n";
  } else {
    std::cout << -1 << "\n";
  }

  return 0;
}
