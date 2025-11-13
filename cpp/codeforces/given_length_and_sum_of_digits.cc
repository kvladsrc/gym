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

string largest(int m, int s) {
  string res;
  int digit = 9;
  if (!s && m > 1) return "-1";

  while (m) {
    if (s >= digit) {
      s -= digit;
      m--;
      res += std::to_string(digit);
    } else {
      digit--;
    }
  }

  if (s) return "-1";
  return res;
}

string smallest(int m, int s) {
  string res;
  int digit = 9;
  if (!s && m > 1) return "-1";

  while (s >= 10) {
    if (!m) return "-1";

    if (s >= digit) {
      s -= digit;
      m--;
      res = std::to_string(digit) + res;
    } else {
      digit--;
    }
  }

  if (!m) return "-1";

  if (m == 1) {
    res = std::to_string(s) + res;
    s = 0;
  } else {
    res = std::to_string(s - 1) + res;
    m--;
    while (m > 1) {
      res = std::to_string(0) + res;
      m--;
    }

    res = std::to_string(1) + res;
    s = 0;
  }

  if (s) return "-1";
  return res;
}

int main(int argc, char *argv[]) {
  int m, s;
  std::cin >> m >> s;
  std::cout << smallest(m, s) << " ";
  std::cout << largest(m, s) << "\n";

  return 0;
}
