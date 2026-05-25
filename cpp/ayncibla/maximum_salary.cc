#include <boost/range/algorithm/sort.hpp>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

using std::string;
using std::vector;

static bool isBetter(const std::string& a, const std::string& b) {
  std::stringstream first;
  std::stringstream second;

  first << a << b;
  second << b << a;

  return std::stoi(first.str()) > std::stoi(second.str());
}

static string largest_number(vector<string> a) {
  boost::range::sort(a, isBetter);

  std::stringstream ret;
  for (const auto& i : a) {
    ret << i;
  }
  string result;
  ret >> result;
  return result;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<string> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }
  std::cout << largest_number(a);
}
