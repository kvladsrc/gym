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

int main(int argc, char* argv[]) {
  string n;
  std::cin >> n;

  int fours_before = 0;
  bool res = true;

  for (char c : n) {
    if (c != '1' && c != '4') {
      res = false;
      break;
    }

    if (c == '4') {
      if (fours_before >= 2) {
        res = false;
        break;
      }
      fours_before++;
      continue;
    }

    fours_before = 0;
  }

  if (res && n[0] == '1') {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
