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

// Check if a string is a square string. Example: "abab" is a square
// string.
bool is_square(const string& in) {
  int n = in.size();
  if (n % 2 != 0) {
    return false;
  }
  int half = n / 2;
  string first = in.substr(0, half);
  string second = in.substr(half, half);
  return first == second;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string input;
    std::cin >> input;
    if (is_square(input)) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }
  return 0;
}
