#include <algorithm>
#include <cstddef>
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
  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;
    string result;
    for (size_t i = 0; i < s.size(); i += 2) {
      result += s[i];
    }
    result += s[s.size() - 1];
    std::cout << result << std::endl;
  }
  return 0;
}
