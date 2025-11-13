#include <algorithm>
#include <cstdlib>
#include <ctime>
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

int moves(char a, char b) {
  int want = a - '0';
  int got = b - '0';
  int up = (got - want + 10) % 10;
  int down = (want - got + 10) % 10;
  return min(up, down);
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  string want;
  string got;
  std::cin >> want >> got;

  int res = 0;
  for (size_t i = 0; i < want.size(); ++i) {
    res += moves(want[i], got[i]);
  }

  std::cout << res << "\n";

  return 0;
}
