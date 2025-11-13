#include <algorithm>
#include <cstdint>
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

int main(int argc, char *argv[]) {
  int a1, a2, a3, a4;
  std::cin >> a1 >> a2 >> a3 >> a4;

  int64_t res = 0;
  string in;
  std::cin >> in;

  for (char c : in) {
    switch (c - '0') {
      case 1:
        res += a1;
        break;
      case 2:
        res += a2;
        break;
      case 3:
        res += a3;
        break;
      default:
        res += a4;
    }
  }
  std::cout << res << "\n";

  return 0;
}
