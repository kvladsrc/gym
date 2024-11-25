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

int solve(int boring_number) {
  int called_before_seq = ((boring_number % 10) - 1) * 10;
  int digits = 0;

  while (boring_number) {
    digits++;
    boring_number /= 10;
  }

  switch (digits) {
    case 1:
      called_before_seq += 1;
      break;
    case 2:
      called_before_seq += 1 + 2;
      break;
    case 3:
      called_before_seq += 1 + 2 + 3;
      break;
    default:
      called_before_seq += 1 + 2 + 3 + 4;
  }

  return called_before_seq;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int boring_number;
    std::cin >> boring_number;
    std::cout << solve(boring_number) << "\n";
  }
  return 0;
}
