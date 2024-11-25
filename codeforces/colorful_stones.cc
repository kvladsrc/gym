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

int main(int argc, char *argv[]) {
  string s, t;
  std::cin >> s >> t;
  int pos = 0;
  for (char c : t) {
    if (c == s[pos]) {
      pos++;
    }
  }
  std::cout << pos + 1 << "\n";

  return 0;
}
